-- =============================================
-- Author:      Database Automation Platform Team
-- Create Date: 2025-06-21
-- Description: Cleanup old audit log entries based on retention policy
-- =============================================
CREATE OR ALTER PROCEDURE audit.sp_AuditLog_Cleanup
    @RetentionDays INT = NULL,
    @BatchSize INT = 1000,
    @MaxExecutionTimeSeconds INT = 300,
    @DryRun BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StartTime DATETIME2(7) = SYSUTCDATETIME();
    DECLARE @DeletedCount INT = 0;
    DECLARE @TotalDeleted INT = 0;
    DECLARE @CutoffDate DATETIME2(7);
    DECLARE @ConfigRetentionDays INT;
    
    BEGIN TRY
        -- Get retention days from configuration if not provided
        IF @RetentionDays IS NULL
        BEGIN
            SELECT @ConfigRetentionDays = CAST([Value] AS INT)
            FROM system.Configuration
            WHERE Category = 'Audit' AND [Key] = 'RetentionDays' AND IsActive = 1;
            
            SET @RetentionDays = ISNULL(@ConfigRetentionDays, 2555); -- Default 7 years
        END
        
        -- Validate retention days
        IF @RetentionDays < 90
        BEGIN
            RAISERROR('Retention days cannot be less than 90 days for compliance', 16, 1);
            RETURN -1;
        END
        
        -- Calculate cutoff date
        SET @CutoffDate = DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
        
        -- Log cleanup start
        IF @DryRun = 0
        BEGIN
            EXEC audit.sp_AuditLog_Insert
                @EventType = 'Maintenance',
                @EventSubType = 'AuditLogCleanup',
                @CorrelationId = NEWID(),
                @UserId = SYSTEM_USER,
                @Action = 'StartCleanup',
                @Result = 'Success',
                @AdditionalData = JSON_QUERY(
                    (SELECT 
                        @RetentionDays AS RetentionDays,
                        @CutoffDate AS CutoffDate,
                        @BatchSize AS BatchSize
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
                );
        END
        
        -- Count records to be deleted
        DECLARE @ToDeleteCount INT;
        SELECT @ToDeleteCount = COUNT(*)
        FROM audit.AuditEvents
        WHERE Timestamp < @CutoffDate;
        
        IF @DryRun = 1
        BEGIN
            -- Dry run - just return what would be deleted
            SELECT 
                @ToDeleteCount AS RecordsToDelete,
                @CutoffDate AS CutoffDate,
                @RetentionDays AS RetentionDays,
                MIN(Timestamp) AS OldestRecord,
                MAX(Timestamp) AS NewestRecordToDelete
            FROM audit.AuditEvents
            WHERE Timestamp < @CutoffDate;
            
            RETURN 0;
        END
        
        -- Perform deletion in batches
        WHILE @DeletedCount > 0 OR @TotalDeleted = 0
        BEGIN
            -- Check execution time
            IF DATEDIFF(SECOND, @StartTime, SYSUTCDATETIME()) > @MaxExecutionTimeSeconds
            BEGIN
                PRINT 'Maximum execution time reached. Cleanup will continue in next run.';
                BREAK;
            END
            
            -- Delete batch
            DELETE TOP (@BatchSize) 
            FROM audit.AuditEvents
            WHERE Timestamp < @CutoffDate;
            
            SET @DeletedCount = @@ROWCOUNT;
            SET @TotalDeleted = @TotalDeleted + @DeletedCount;
            
            -- Brief pause to avoid blocking
            IF @DeletedCount > 0
            BEGIN
                WAITFOR DELAY '00:00:00.100'; -- 100ms pause
            END
        END
        
        -- Log cleanup completion
        EXEC audit.sp_AuditLog_Insert
            @EventType = 'Maintenance',
            @EventSubType = 'AuditLogCleanup',
            @CorrelationId = NEWID(),
            @UserId = SYSTEM_USER,
            @Action = 'CompleteCleanup',
            @Result = 'Success',
            @Duration = DATEDIFF(MILLISECOND, @StartTime, SYSUTCDATETIME()),
            @AdditionalData = JSON_QUERY(
                (SELECT 
                    @TotalDeleted AS RecordsDeleted,
                    @ToDeleteCount AS TotalRecordsToDelete,
                    @RetentionDays AS RetentionDays,
                    @CutoffDate AS CutoffDate
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
            );
        
        -- Return summary
        SELECT 
            @TotalDeleted AS RecordsDeleted,
            @ToDeleteCount AS TotalRecordsToDelete,
            @RetentionDays AS RetentionDays,
            @CutoffDate AS CutoffDate,
            DATEDIFF(SECOND, @StartTime, SYSUTCDATETIME()) AS ExecutionTimeSeconds;
            
    END TRY
    BEGIN CATCH
        -- Log error
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        
        EXEC audit.sp_AuditLog_Insert
            @EventType = 'Maintenance',
            @EventSubType = 'AuditLogCleanup',
            @CorrelationId = NEWID(),
            @UserId = SYSTEM_USER,
            @Action = 'CleanupError',
            @Result = 'Failure',
            @ErrorCode = CAST(ERROR_NUMBER() AS NVARCHAR(50)),
            @ErrorMessage = @ErrorMessage,
            @Duration = DATEDIFF(MILLISECOND, @StartTime, SYSUTCDATETIME());
        
        -- Re-throw error
        THROW;
    END CATCH
END
GO

-- Grant execute permission to DBA role
GRANT EXECUTE ON audit.sp_AuditLog_Cleanup TO db_dba;
GO