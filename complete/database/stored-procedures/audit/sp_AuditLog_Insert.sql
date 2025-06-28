-- =============================================
-- Author:      Database Automation Platform Team
-- Create Date: 2025-06-21
-- Description: Insert an audit event into the audit log
-- =============================================
CREATE OR ALTER PROCEDURE audit.sp_AuditLog_Insert
    @EventType NVARCHAR(100),
    @EventSubType NVARCHAR(100) = NULL,
    @CorrelationId UNIQUEIDENTIFIER,
    @UserId NVARCHAR(256) = NULL,
    @UserName NVARCHAR(256) = NULL,
    @UserEmail NVARCHAR(256) = NULL,
    @UserRoles NVARCHAR(MAX) = NULL, -- JSON array
    @IpAddress NVARCHAR(45) = NULL,
    @UserAgent NVARCHAR(500) = NULL,
    @ResourceType NVARCHAR(100) = NULL,
    @ResourceId NVARCHAR(256) = NULL,
    @ResourceName NVARCHAR(500) = NULL,
    @Action NVARCHAR(100),
    @Result NVARCHAR(50), -- Success, Failure, PartialSuccess
    @ErrorCode NVARCHAR(50) = NULL,
    @ErrorMessage NVARCHAR(MAX) = NULL,
    @Duration INT = NULL,
    @AdditionalData NVARCHAR(MAX) = NULL, -- JSON
    @MachineName NVARCHAR(256) = NULL,
    @ProcessId INT = NULL,
    @ThreadId INT = NULL,
    @ApplicationVersion NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Validate required parameters
    IF @EventType IS NULL OR LEN(@EventType) = 0
    BEGIN
        RAISERROR('EventType is required', 16, 1);
        RETURN -1;
    END
    
    IF @CorrelationId IS NULL
    BEGIN
        RAISERROR('CorrelationId is required', 16, 1);
        RETURN -1;
    END
    
    IF @Action IS NULL OR LEN(@Action) = 0
    BEGIN
        RAISERROR('Action is required', 16, 1);
        RETURN -1;
    END
    
    IF @Result NOT IN ('Success', 'Failure', 'PartialSuccess')
    BEGIN
        RAISERROR('Result must be Success, Failure, or PartialSuccess', 16, 1);
        RETURN -1;
    END
    
    -- Validate JSON fields if provided
    IF @UserRoles IS NOT NULL AND ISJSON(@UserRoles) = 0
    BEGIN
        RAISERROR('UserRoles must be valid JSON', 16, 1);
        RETURN -1;
    END
    
    IF @AdditionalData IS NOT NULL AND ISJSON(@AdditionalData) = 0
    BEGIN
        RAISERROR('AdditionalData must be valid JSON', 16, 1);
        RETURN -1;
    END
    
    BEGIN TRY
        INSERT INTO audit.AuditEvents (
            EventType,
            EventSubType,
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
            ApplicationVersion
        )
        VALUES (
            @EventType,
            @EventSubType,
            @CorrelationId,
            @UserId,
            @UserName,
            @UserEmail,
            @UserRoles,
            @IpAddress,
            @UserAgent,
            @ResourceType,
            @ResourceId,
            @ResourceName,
            @Action,
            @Result,
            @ErrorCode,
            @ErrorMessage,
            @Duration,
            @AdditionalData,
            @MachineName,
            @ProcessId,
            @ThreadId,
            @ApplicationVersion
        );
        
        -- Return the ID of the inserted record
        SELECT SCOPE_IDENTITY() AS AuditEventId;
        
    END TRY
    BEGIN CATCH
        -- Log error but don't throw - auditing should not break the application
        DECLARE @ErrorMsg NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        -- Try to log the error to a system table or event log
        -- For now, just return error info
        SELECT 
            -1 AS AuditEventId,
            @ErrorMsg AS ErrorMessage,
            @ErrorSeverity AS ErrorSeverity,
            @ErrorState AS ErrorState;
            
        RETURN -1;
    END CATCH
END
GO

-- Grant execute permission to application role
GRANT EXECUTE ON audit.sp_AuditLog_Insert TO db_datareader;
GRANT EXECUTE ON audit.sp_AuditLog_Insert TO db_datawriter;
GO