-- =============================================
-- Author:      Database Automation Platform Team
-- Create Date: 2025-06-21
-- Description: Generate audit report with various aggregations and insights
-- =============================================
CREATE OR ALTER PROCEDURE audit.sp_AuditLog_Report
    @ReportType NVARCHAR(50), -- Summary, UserActivity, SecurityEvents, FailureAnalysis, ResourceUsage
    @StartDate DATETIME2(7) = NULL,
    @EndDate DATETIME2(7) = NULL,
    @UserId NVARCHAR(256) = NULL,
    @TopN INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Default date range if not specified (last 7 days)
    IF @StartDate IS NULL
    BEGIN
        SET @StartDate = DATEADD(DAY, -7, SYSUTCDATETIME());
    END
    
    IF @EndDate IS NULL
    BEGIN
        SET @EndDate = SYSUTCDATETIME();
    END
    
    -- Validate parameters
    IF @TopN < 1 SET @TopN = 1;
    IF @TopN > 100 SET @TopN = 100;
    
    IF @ReportType = 'Summary'
    BEGIN
        -- Overall summary statistics
        SELECT 
            COUNT(*) AS TotalEvents,
            COUNT(DISTINCT UserId) AS UniqueUsers,
            COUNT(DISTINCT CorrelationId) AS UniqueSessions,
            SUM(CASE WHEN Result = 'Success' THEN 1 ELSE 0 END) AS SuccessCount,
            SUM(CASE WHEN Result = 'Failure' THEN 1 ELSE 0 END) AS FailureCount,
            SUM(CASE WHEN Result = 'PartialSuccess' THEN 1 ELSE 0 END) AS PartialSuccessCount,
            CAST(SUM(CASE WHEN Result = 'Success' THEN 1.0 ELSE 0 END) / COUNT(*) * 100 AS DECIMAL(5,2)) AS SuccessRate,
            AVG(Duration) AS AvgDurationMs,
            MAX(Duration) AS MaxDurationMs,
            MIN(Timestamp) AS EarliestEvent,
            MAX(Timestamp) AS LatestEvent
        FROM audit.AuditEvents
        WHERE Timestamp BETWEEN @StartDate AND @EndDate
            AND (@UserId IS NULL OR UserId = @UserId);
        
        -- Event type breakdown
        SELECT 
            EventType,
            COUNT(*) AS EventCount,
            CAST(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER() AS DECIMAL(5,2)) AS Percentage,
            AVG(Duration) AS AvgDurationMs
        FROM audit.AuditEvents
        WHERE Timestamp BETWEEN @StartDate AND @EndDate
            AND (@UserId IS NULL OR UserId = @UserId)
        GROUP BY EventType
        ORDER BY EventCount DESC;
    END
    
    ELSE IF @ReportType = 'UserActivity'
    BEGIN
        -- User activity analysis
        SELECT TOP (@TopN)
            UserId,
            UserName,
            COUNT(*) AS TotalActions,
            COUNT(DISTINCT CAST(Timestamp AS DATE)) AS ActiveDays,
            COUNT(DISTINCT EventType) AS UniqueEventTypes,
            SUM(CASE WHEN Result = 'Success' THEN 1 ELSE 0 END) AS SuccessCount,
            SUM(CASE WHEN Result = 'Failure' THEN 1 ELSE 0 END) AS FailureCount,
            CAST(SUM(CASE WHEN Result = 'Success' THEN 1.0 ELSE 0 END) / COUNT(*) * 100 AS DECIMAL(5,2)) AS SuccessRate,
            AVG(Duration) AS AvgDurationMs,
            MIN(Timestamp) AS FirstActivity,
            MAX(Timestamp) AS LastActivity
        FROM audit.AuditEvents
        WHERE Timestamp BETWEEN @StartDate AND @EndDate
            AND UserId IS NOT NULL
        GROUP BY UserId, UserName
        ORDER BY TotalActions DESC;
        
        -- Hourly activity pattern
        SELECT 
            DATEPART(HOUR, Timestamp) AS HourOfDay,
            COUNT(*) AS EventCount,
            AVG(Duration) AS AvgDurationMs
        FROM audit.AuditEvents
        WHERE Timestamp BETWEEN @StartDate AND @EndDate
            AND (@UserId IS NULL OR UserId = @UserId)
        GROUP BY DATEPART(HOUR, Timestamp)
        ORDER BY HourOfDay;
    END
    
    ELSE IF @ReportType = 'SecurityEvents'
    BEGIN
        -- Security-related events
        SELECT TOP (@TopN)
            EventType,
            Action,
            Result,
            COUNT(*) AS EventCount,
            COUNT(DISTINCT UserId) AS UniqueUsers,
            COUNT(DISTINCT IpAddress) AS UniqueIPs,
            STRING_AGG(DISTINCT ErrorCode, ', ') AS ErrorCodes
        FROM audit.AuditEvents
        WHERE Timestamp BETWEEN @StartDate AND @EndDate
            AND EventType IN ('Security', 'Authentication', 'Authorization', 'DataAccess')
        GROUP BY EventType, Action, Result
        ORDER BY EventCount DESC;
        
        -- Failed security events by user
        SELECT TOP (@TopN)
            UserId,
            UserName,
            COUNT(*) AS FailureCount,
            COUNT(DISTINCT IpAddress) AS UniqueIPs,
            STRING_AGG(DISTINCT Action, ', ') AS FailedActions,
            MAX(Timestamp) AS LastFailure
        FROM audit.AuditEvents
        WHERE Timestamp BETWEEN @StartDate AND @EndDate
            AND EventType IN ('Security', 'Authentication', 'Authorization')
            AND Result = 'Failure'
        GROUP BY UserId, UserName
        ORDER BY FailureCount DESC;
    END
    
    ELSE IF @ReportType = 'FailureAnalysis'
    BEGIN
        -- Failure analysis
        SELECT TOP (@TopN)
            EventType,
            Action,
            ErrorCode,
            COUNT(*) AS FailureCount,
            COUNT(DISTINCT UserId) AS AffectedUsers,
            AVG(Duration) AS AvgDurationMs,
            MIN(Timestamp) AS FirstOccurrence,
            MAX(Timestamp) AS LastOccurrence
        FROM audit.AuditEvents
        WHERE Timestamp BETWEEN @StartDate AND @EndDate
            AND Result = 'Failure'
            AND (@UserId IS NULL OR UserId = @UserId)
        GROUP BY EventType, Action, ErrorCode
        ORDER BY FailureCount DESC;
        
        -- Error message patterns (top 10)
        SELECT TOP 10
            ErrorCode,
            LEFT(ErrorMessage, 200) AS ErrorMessageSample,
            COUNT(*) AS OccurrenceCount
        FROM audit.AuditEvents
        WHERE Timestamp BETWEEN @StartDate AND @EndDate
            AND Result = 'Failure'
            AND ErrorMessage IS NOT NULL
            AND (@UserId IS NULL OR UserId = @UserId)
        GROUP BY ErrorCode, LEFT(ErrorMessage, 200)
        ORDER BY OccurrenceCount DESC;
    END
    
    ELSE IF @ReportType = 'ResourceUsage'
    BEGIN
        -- Resource usage analysis
        SELECT TOP (@TopN)
            ResourceType,
            ResourceName,
            COUNT(*) AS AccessCount,
            COUNT(DISTINCT UserId) AS UniqueUsers,
            COUNT(DISTINCT Action) AS UniqueActions,
            AVG(Duration) AS AvgDurationMs,
            SUM(Duration) AS TotalDurationMs
        FROM audit.AuditEvents
        WHERE Timestamp BETWEEN @StartDate AND @EndDate
            AND ResourceType IS NOT NULL
            AND (@UserId IS NULL OR UserId = @UserId)
        GROUP BY ResourceType, ResourceName
        ORDER BY AccessCount DESC;
        
        -- Resource access by time
        SELECT 
            CAST(Timestamp AS DATE) AS AccessDate,
            ResourceType,
            COUNT(*) AS AccessCount,
            COUNT(DISTINCT ResourceId) AS UniqueResources,
            COUNT(DISTINCT UserId) AS UniqueUsers
        FROM audit.AuditEvents
        WHERE Timestamp BETWEEN @StartDate AND @EndDate
            AND ResourceType IS NOT NULL
            AND (@UserId IS NULL OR UserId = @UserId)
        GROUP BY CAST(Timestamp AS DATE), ResourceType
        ORDER BY AccessDate DESC, ResourceType;
    END
    
    ELSE
    BEGIN
        RAISERROR('Invalid ReportType. Valid values: Summary, UserActivity, SecurityEvents, FailureAnalysis, ResourceUsage', 16, 1);
        RETURN -1;
    END
    
END
GO

-- Grant execute permission
GRANT EXECUTE ON audit.sp_AuditLog_Report TO db_auditor;
GRANT EXECUTE ON audit.sp_AuditLog_Report TO db_security_admin;
GRANT EXECUTE ON audit.sp_AuditLog_Report TO db_dba;
GO