-- =============================================
-- Database Automation Platform - Core Schema
-- Version: 1.0
-- Created: 2025-06-21
-- Description: Core database schema including audit, security, and system tables
-- =============================================

-- Create schemas
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'audit')
BEGIN
    EXEC('CREATE SCHEMA audit AUTHORIZATION dbo');
END
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'security')
BEGIN
    EXEC('CREATE SCHEMA security AUTHORIZATION dbo');
END
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'system')
BEGIN
    EXEC('CREATE SCHEMA system AUTHORIZATION dbo');
END
GO

-- =============================================
-- Audit Schema Tables
-- =============================================

-- Drop existing tables if they exist (for development only)
IF OBJECT_ID('audit.AuditEvents', 'U') IS NOT NULL
    DROP TABLE audit.AuditEvents;
GO

-- Create AuditEvents table
CREATE TABLE audit.AuditEvents
(
    Id BIGINT IDENTITY(1,1) NOT NULL,
    EventType NVARCHAR(100) NOT NULL,
    EventSubType NVARCHAR(100) NULL,
    Timestamp DATETIME2(7) NOT NULL CONSTRAINT DF_AuditEvents_Timestamp DEFAULT SYSUTCDATETIME(),
    CorrelationId UNIQUEIDENTIFIER NOT NULL,
    UserId NVARCHAR(256) NULL,
    UserName NVARCHAR(256) NULL,
    UserEmail NVARCHAR(256) NULL,
    UserRoles NVARCHAR(MAX) NULL, -- JSON array of roles
    IpAddress NVARCHAR(45) NULL,
    UserAgent NVARCHAR(500) NULL,
    ResourceType NVARCHAR(100) NULL,
    ResourceId NVARCHAR(256) NULL,
    ResourceName NVARCHAR(500) NULL,
    Action NVARCHAR(100) NOT NULL,
    Result NVARCHAR(50) NOT NULL, -- Success, Failure, PartialSuccess
    ErrorCode NVARCHAR(50) NULL,
    ErrorMessage NVARCHAR(MAX) NULL,
    Duration INT NULL, -- milliseconds
    AdditionalData NVARCHAR(MAX) NULL, -- JSON for extensibility
    MachineName NVARCHAR(256) NULL,
    ProcessId INT NULL,
    ThreadId INT NULL,
    ApplicationVersion NVARCHAR(50) NULL,
    CONSTRAINT PK_AuditEvents PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_AuditEvents_Result CHECK (Result IN ('Success', 'Failure', 'PartialSuccess'))
);
GO

-- Create indexes for common queries
CREATE NONCLUSTERED INDEX IX_AuditEvents_Timestamp ON audit.AuditEvents(Timestamp DESC) INCLUDE (EventType, UserId, Result);
CREATE NONCLUSTERED INDEX IX_AuditEvents_CorrelationId ON audit.AuditEvents(CorrelationId);
CREATE NONCLUSTERED INDEX IX_AuditEvents_UserId ON audit.AuditEvents(UserId) WHERE UserId IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_AuditEvents_EventType ON audit.AuditEvents(EventType, Timestamp DESC);
CREATE NONCLUSTERED INDEX IX_AuditEvents_ResourceType_ResourceId ON audit.AuditEvents(ResourceType, ResourceId) WHERE ResourceType IS NOT NULL;
GO

-- =============================================
-- Security Schema Tables
-- =============================================

-- Drop existing tables if they exist (for development only)
IF OBJECT_ID('security.DataClassifications', 'U') IS NOT NULL
    DROP TABLE security.DataClassifications;
GO

IF OBJECT_ID('security.AccessPatterns', 'U') IS NOT NULL
    DROP TABLE security.AccessPatterns;
GO

IF OBJECT_ID('security.SecurityIncidents', 'U') IS NOT NULL
    DROP TABLE security.SecurityIncidents;
GO

-- Create DataClassifications table
CREATE TABLE security.DataClassifications
(
    Id INT IDENTITY(1,1) NOT NULL,
    DatabaseName NVARCHAR(128) NOT NULL,
    SchemaName NVARCHAR(128) NOT NULL,
    TableName NVARCHAR(128) NOT NULL,
    ColumnName NVARCHAR(128) NOT NULL,
    ClassificationType NVARCHAR(50) NOT NULL, -- PII, PHI, PCI, Confidential, Public
    ClassificationSubType NVARCHAR(100) NULL, -- Email, SSN, CreditCard, etc.
    SensitivityLevel INT NOT NULL, -- 1-5 (1=Public, 5=Highly Sensitive)
    DiscoveredDate DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    LastValidatedDate DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    IsActive BIT NOT NULL DEFAULT 1,
    ValidationMethod NVARCHAR(50) NOT NULL, -- Automated, Manual, Hybrid
    Confidence DECIMAL(5,2) NOT NULL, -- 0.00 to 100.00
    Notes NVARCHAR(MAX) NULL,
    CreatedBy NVARCHAR(256) NOT NULL,
    CreatedDate DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedBy NVARCHAR(256) NULL,
    ModifiedDate DATETIME2(7) NULL,
    CONSTRAINT PK_DataClassifications PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_DataClassifications_Column UNIQUE (DatabaseName, SchemaName, TableName, ColumnName),
    CONSTRAINT CK_DataClassifications_SensitivityLevel CHECK (SensitivityLevel BETWEEN 1 AND 5),
    CONSTRAINT CK_DataClassifications_Confidence CHECK (Confidence BETWEEN 0 AND 100)
);
GO

-- Create AccessPatterns table for monitoring data access
CREATE TABLE security.AccessPatterns
(
    Id BIGINT IDENTITY(1,1) NOT NULL,
    Timestamp DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId NVARCHAR(256) NOT NULL,
    DatabaseName NVARCHAR(128) NOT NULL,
    SchemaName NVARCHAR(128) NULL,
    ObjectName NVARCHAR(128) NULL,
    ObjectType NVARCHAR(50) NULL, -- Table, View, StoredProcedure, Function
    Operation NVARCHAR(50) NOT NULL, -- SELECT, INSERT, UPDATE, DELETE, EXECUTE
    RowCount INT NULL,
    ExecutionTime INT NULL, -- milliseconds
    QueryHash VARBINARY(32) NULL,
    IsSensitiveData BIT NOT NULL DEFAULT 0,
    DataClassifications NVARCHAR(MAX) NULL, -- JSON array of classifications accessed
    ClientIpAddress NVARCHAR(45) NULL,
    ApplicationName NVARCHAR(256) NULL,
    CONSTRAINT PK_AccessPatterns PRIMARY KEY CLUSTERED (Id)
);
GO

-- Create indexes for access pattern analysis
CREATE NONCLUSTERED INDEX IX_AccessPatterns_Timestamp ON security.AccessPatterns(Timestamp DESC);
CREATE NONCLUSTERED INDEX IX_AccessPatterns_UserId ON security.AccessPatterns(UserId, Timestamp DESC);
CREATE NONCLUSTERED INDEX IX_AccessPatterns_Object ON security.AccessPatterns(DatabaseName, SchemaName, ObjectName) WHERE ObjectName IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_AccessPatterns_SensitiveData ON security.AccessPatterns(IsSensitiveData, Timestamp DESC) WHERE IsSensitiveData = 1;
GO

-- Create SecurityIncidents table
CREATE TABLE security.SecurityIncidents
(
    Id INT IDENTITY(1,1) NOT NULL,
    IncidentType NVARCHAR(100) NOT NULL, -- UnauthorizedAccess, DataExfiltration, SQLInjection, etc.
    Severity NVARCHAR(20) NOT NULL, -- Critical, High, Medium, Low
    DetectedDate DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId NVARCHAR(256) NULL,
    SourceIpAddress NVARCHAR(45) NULL,
    TargetResource NVARCHAR(500) NULL,
    Description NVARCHAR(MAX) NOT NULL,
    DetectionMethod NVARCHAR(100) NOT NULL, -- Automated, Manual, UserReport
    Status NVARCHAR(50) NOT NULL DEFAULT 'Open', -- Open, Investigating, Resolved, Closed
    AssignedTo NVARCHAR(256) NULL,
    ResolutionDate DATETIME2(7) NULL,
    ResolutionNotes NVARCHAR(MAX) NULL,
    CorrelationId UNIQUEIDENTIFIER NULL,
    RelatedAuditEventIds NVARCHAR(MAX) NULL, -- JSON array of audit event IDs
    CONSTRAINT PK_SecurityIncidents PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_SecurityIncidents_Severity CHECK (Severity IN ('Critical', 'High', 'Medium', 'Low')),
    CONSTRAINT CK_SecurityIncidents_Status CHECK (Status IN ('Open', 'Investigating', 'Resolved', 'Closed'))
);
GO

-- =============================================
-- System Schema Tables
-- =============================================

-- Drop existing tables if they exist (for development only)
IF OBJECT_ID('system.Configuration', 'U') IS NOT NULL
    DROP TABLE system.Configuration;
GO

IF OBJECT_ID('system.HealthChecks', 'U') IS NOT NULL
    DROP TABLE system.HealthChecks;
GO

IF OBJECT_ID('system.MaintenanceLog', 'U') IS NOT NULL
    DROP TABLE system.MaintenanceLog;
GO

-- Create Configuration table
CREATE TABLE system.Configuration
(
    Id INT IDENTITY(1,1) NOT NULL,
    Category NVARCHAR(100) NOT NULL,
    [Key] NVARCHAR(256) NOT NULL,
    [Value] NVARCHAR(MAX) NOT NULL,
    DataType NVARCHAR(50) NOT NULL DEFAULT 'String', -- String, Int, Boolean, JSON
    Description NVARCHAR(500) NULL,
    IsEncrypted BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedDate DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(256) NOT NULL,
    ModifiedDate DATETIME2(7) NULL,
    ModifiedBy NVARCHAR(256) NULL,
    CONSTRAINT PK_Configuration PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Configuration_CategoryKey UNIQUE (Category, [Key])
);
GO

-- Create HealthChecks table
CREATE TABLE system.HealthChecks
(
    Id BIGINT IDENTITY(1,1) NOT NULL,
    CheckName NVARCHAR(100) NOT NULL,
    CheckType NVARCHAR(50) NOT NULL, -- Database, API, Service, External
    Timestamp DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    Status NVARCHAR(20) NOT NULL, -- Healthy, Degraded, Unhealthy
    ResponseTime INT NULL, -- milliseconds
    Details NVARCHAR(MAX) NULL, -- JSON with detailed health information
    ErrorMessage NVARCHAR(MAX) NULL,
    CONSTRAINT PK_HealthChecks PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_HealthChecks_Status CHECK (Status IN ('Healthy', 'Degraded', 'Unhealthy'))
);
GO

-- Create index for health check queries
CREATE NONCLUSTERED INDEX IX_HealthChecks_CheckName_Timestamp ON system.HealthChecks(CheckName, Timestamp DESC);
GO

-- Create MaintenanceLog table
CREATE TABLE system.MaintenanceLog
(
    Id INT IDENTITY(1,1) NOT NULL,
    OperationType NVARCHAR(100) NOT NULL, -- IndexRebuild, StatisticsUpdate, BackupValidation, etc.
    ObjectName NVARCHAR(500) NULL,
    StartTime DATETIME2(7) NOT NULL,
    EndTime DATETIME2(7) NULL,
    Duration AS DATEDIFF(SECOND, StartTime, EndTime),
    Status NVARCHAR(20) NOT NULL DEFAULT 'Running', -- Running, Completed, Failed, Cancelled
    ErrorMessage NVARCHAR(MAX) NULL,
    PerformedBy NVARCHAR(256) NOT NULL,
    Details NVARCHAR(MAX) NULL, -- JSON for operation-specific details
    CONSTRAINT PK_MaintenanceLog PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_MaintenanceLog_Status CHECK (Status IN ('Running', 'Completed', 'Failed', 'Cancelled'))
);
GO

-- =============================================
-- Row-Level Security Policies
-- =============================================

-- Create security predicate function for audit events
CREATE FUNCTION audit.fn_SecurityPredicate(@UserId NVARCHAR(256))
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT 1 AS Result
    WHERE 
        -- User can see their own audit events
        @UserId = USER_NAME()
        OR
        -- Auditors can see all events
        IS_ROLEMEMBER('db_auditor') = 1
        OR
        -- Admins can see all events
        IS_ROLEMEMBER('db_owner') = 1;
GO

-- Create security policy for audit events
CREATE SECURITY POLICY audit.AuditEventPolicy
ADD FILTER PREDICATE audit.fn_SecurityPredicate(UserId) ON audit.AuditEvents
WITH (STATE = ON);
GO

-- =============================================
-- Default Data
-- =============================================

-- Insert default configuration values
INSERT INTO system.Configuration (Category, [Key], [Value], DataType, Description, CreatedBy)
VALUES 
    ('Audit', 'RetentionDays', '2555', 'Int', 'Number of days to retain audit logs (7 years)', 'SYSTEM'),
    ('Audit', 'EnableDetailedLogging', 'true', 'Boolean', 'Enable detailed audit logging', 'SYSTEM'),
    ('Security', 'MaxFailedLoginAttempts', '5', 'Int', 'Maximum failed login attempts before lockout', 'SYSTEM'),
    ('Security', 'SessionTimeoutMinutes', '60', 'Int', 'Session timeout in minutes', 'SYSTEM'),
    ('Security', 'RequireMFA', 'true', 'Boolean', 'Require multi-factor authentication', 'SYSTEM'),
    ('Performance', 'QueryTimeoutSeconds', '300', 'Int', 'Default query timeout in seconds', 'SYSTEM'),
    ('Performance', 'MaxConcurrentConnections', '100', 'Int', 'Maximum concurrent connections per service', 'SYSTEM'),
    ('Maintenance', 'BackupValidationEnabled', 'true', 'Boolean', 'Enable automated backup validation', 'SYSTEM'),
    ('Maintenance', 'IndexMaintenanceThreshold', '30', 'Int', 'Fragmentation percentage threshold for index maintenance', 'SYSTEM');
GO

-- =============================================
-- Database Roles
-- =============================================

-- Create custom database roles
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'db_auditor' AND type = 'R')
BEGIN
    CREATE ROLE db_auditor;
END
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'db_security_admin' AND type = 'R')
BEGIN
    CREATE ROLE db_security_admin;
END
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'db_developer' AND type = 'R')
BEGIN
    CREATE ROLE db_developer;
END
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'db_dba' AND type = 'R')
BEGIN
    CREATE ROLE db_dba;
END
GO

-- Grant permissions to roles
-- Auditor role
GRANT SELECT ON SCHEMA::audit TO db_auditor;
GRANT SELECT ON SCHEMA::security TO db_auditor;
GRANT SELECT ON SCHEMA::system TO db_auditor;

-- Security Admin role
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::security TO db_security_admin;
GRANT SELECT ON SCHEMA::audit TO db_security_admin;
GRANT EXECUTE ON SCHEMA::security TO db_security_admin;

-- Developer role
GRANT SELECT ON SCHEMA::audit TO db_developer;
GRANT SELECT ON security.DataClassifications TO db_developer;
GRANT SELECT ON system.Configuration TO db_developer;

-- DBA role
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::system TO db_dba;
GRANT SELECT ON SCHEMA::audit TO db_dba;
GRANT SELECT ON SCHEMA::security TO db_dba;
GRANT EXECUTE ON SCHEMA::system TO db_dba;
GO

-- =============================================
-- Script Completion
-- =============================================
PRINT 'Core schema creation completed successfully';
PRINT 'Created schemas: audit, security, system';
PRINT 'Created tables: AuditEvents, DataClassifications, AccessPatterns, SecurityIncidents, Configuration, HealthChecks, MaintenanceLog';
PRINT 'Created roles: db_auditor, db_security_admin, db_developer, db_dba';
GO