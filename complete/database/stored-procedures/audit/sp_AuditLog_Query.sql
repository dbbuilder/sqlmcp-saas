-- =============================================
-- Author:      Database Automation Platform Team
-- Create Date: 2025-06-21
-- Description: Query audit events with flexible filtering and pagination
-- =============================================
CREATE OR ALTER PROCEDURE audit.sp_AuditLog_Query
    @StartDate DATETIME2(7) = NULL,
    @EndDate DATETIME2(7) = NULL,
    @EventType NVARCHAR(100) = NULL,
    @UserId NVARCHAR(256) = NULL,
    @CorrelationId UNIQUEIDENTIFIER = NULL,
    @ResourceType NVARCHAR(100) = NULL,
    @ResourceId NVARCHAR(256) = NULL,
    @Action NVARCHAR(100) = NULL,
    @Result NVARCHAR(50) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 100,
    @SortBy NVARCHAR(50) = 'Timestamp',
    @SortDirection NVARCHAR(4) = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Validate parameters
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 1;
    IF @PageSize > 1000 SET @PageSize = 1000; -- Max page size
    
    -- Default date range if not specified (last 24 hours)
    IF @StartDate IS NULL AND @EndDate IS NULL
    BEGIN
        SET @EndDate = SYSUTCDATETIME();
        SET @StartDate = DATEADD(HOUR, -24, @EndDate);
    END
    ELSE IF @StartDate IS NULL
    BEGIN
        SET @StartDate = DATEADD(DAY, -7, @EndDate); -- Max 7 days back if only end date specified
    END
    ELSE IF @EndDate IS NULL
    BEGIN
        SET @EndDate = SYSUTCDATETIME();
    END
    
    -- Validate sort parameters
    IF @SortBy NOT IN ('Timestamp', 'EventType', 'UserId', 'Result', 'Duration')
    BEGIN
        SET @SortBy = 'Timestamp';
    END
    
    IF UPPER(@SortDirection) NOT IN ('ASC', 'DESC')
    BEGIN
        SET @SortDirection = 'DESC';
    END
    
    -- Calculate offset
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    
    -- Build dynamic query
    DECLARE @sql NVARCHAR(MAX);
    DECLARE @params NVARCHAR(MAX);
    
    SET @sql = N'
    WITH AuditCTE AS (
        SELECT 
            Id,
            EventType,
            EventSubType,
            Timestamp,
            CorrelationId,
            UserId,
            UserName,
            UserEmail,
            UserRoles,
            IpAddress,
            UserAgent,
            ResourceType,
            ResourceId,
            ResourceName,
            Action,
            Result,
            ErrorCode,
            ErrorMessage,
            Duration,
            AdditionalData,
            MachineName,
            ProcessId,
            ThreadId,
            ApplicationVersion,
            COUNT(*) OVER() AS TotalRecords
        FROM audit.AuditEvents
        WHERE Timestamp >= @StartDate AND Timestamp <= @EndDate';
    
    -- Add optional filters
    IF @EventType IS NOT NULL
        SET @sql = @sql + N' AND EventType = @EventType';
    
    IF @UserId IS NOT NULL
        SET @sql = @sql + N' AND UserId = @UserId';
    
    IF @CorrelationId IS NOT NULL
        SET @sql = @sql + N' AND CorrelationId = @CorrelationId';
    
    IF @ResourceType IS NOT NULL
        SET @sql = @sql + N' AND ResourceType = @ResourceType';
    
    IF @ResourceId IS NOT NULL
        SET @sql = @sql + N' AND ResourceId = @ResourceId';
    
    IF @Action IS NOT NULL
        SET @sql = @sql + N' AND Action = @Action';
    
    IF @Result IS NOT NULL
        SET @sql = @sql + N' AND Result = @Result';
    
    -- Add sorting and pagination
    SET @sql = @sql + N'
    )
    SELECT 
        Id,
        EventType,
        EventSubType,
        Timestamp,
        CorrelationId,
        UserId,
        UserName,
        UserEmail,
        UserRoles,
        IpAddress,
        UserAgent,
        ResourceType,
        ResourceId,
        ResourceName,
        Action,
        Result,
        ErrorCode,
        ErrorMessage,
        Duration,
        AdditionalData,
        MachineName,
        ProcessId,
        ThreadId,
        ApplicationVersion,
        TotalRecords,
        @PageNumber AS PageNumber,
        @PageSize AS PageSize,
        CEILING(CAST(TotalRecords AS FLOAT) / @PageSize) AS TotalPages
    FROM AuditCTE
    ORDER BY ' + QUOTENAME(@SortBy) + ' ' + @SortDirection + N'
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;';
    
    -- Define parameters
    SET @params = N'@StartDate DATETIME2(7), @EndDate DATETIME2(7), @EventType NVARCHAR(100), 
                    @UserId NVARCHAR(256), @CorrelationId UNIQUEIDENTIFIER, @ResourceType NVARCHAR(100),
                    @ResourceId NVARCHAR(256), @Action NVARCHAR(100), @Result NVARCHAR(50),
                    @PageNumber INT, @PageSize INT, @Offset INT';
    
    -- Execute dynamic query
    BEGIN TRY
        EXEC sp_executesql @sql, @params,
            @StartDate = @StartDate,
            @EndDate = @EndDate,
            @EventType = @EventType,
            @UserId = @UserId,
            @CorrelationId = @CorrelationId,
            @ResourceType = @ResourceType,
            @ResourceId = @ResourceId,
            @Action = @Action,
            @Result = @Result,
            @PageNumber = @PageNumber,
            @PageSize = @PageSize,
            @Offset = @Offset;
    END TRY
    BEGIN CATCH
        -- Return error information
        SELECT 
            ERROR_NUMBER() AS ErrorNumber,
            ERROR_MESSAGE() AS ErrorMessage,
            ERROR_SEVERITY() AS ErrorSeverity,
            ERROR_STATE() AS ErrorState,
            ERROR_LINE() AS ErrorLine;
        
        RETURN -1;
    END CATCH
END
GO

-- Grant execute permission
GRANT EXECUTE ON audit.sp_AuditLog_Query TO db_datareader;
GRANT EXECUTE ON audit.sp_AuditLog_Query TO db_auditor;
GO