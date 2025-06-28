-- =============================================
-- Database Automation Platform - Initial Migration
-- Version: 001
-- Created: 2025-06-21
-- Description: Initial database setup and migration tracking
-- =============================================

-- Create migration tracking table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'migration')
BEGIN
    EXEC('CREATE SCHEMA migration AUTHORIZATION dbo');
END
GO

IF OBJECT_ID('migration.MigrationHistory', 'U') IS NULL
BEGIN
    CREATE TABLE migration.MigrationHistory
    (
        Id INT IDENTITY(1,1) NOT NULL,
        MigrationId NVARCHAR(100) NOT NULL,
        ScriptName NVARCHAR(256) NOT NULL,
        AppliedDate DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        AppliedBy NVARCHAR(256) NOT NULL DEFAULT SYSTEM_USER,
        ExecutionTime INT NULL, -- milliseconds
        Checksum NVARCHAR(64) NULL,
        Success BIT NOT NULL DEFAULT 1,
        ErrorMessage NVARCHAR(MAX) NULL,
        CONSTRAINT PK_MigrationHistory PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_MigrationHistory_MigrationId UNIQUE (MigrationId)
    );
END
GO

-- Check if this migration has already been applied
IF EXISTS (SELECT 1 FROM migration.MigrationHistory WHERE MigrationId = '001_Initial_Setup')
BEGIN
    PRINT 'Migration 001_Initial_Setup has already been applied. Skipping...';
    RETURN;
END
GO

-- Begin migration
DECLARE @StartTime DATETIME2(7) = SYSUTCDATETIME();
DECLARE @ErrorMessage NVARCHAR(MAX);

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT 'Starting migration 001_Initial_Setup...';

    -- Execute the core schema script
    PRINT 'Creating core database schema...';
    -- Note: In production, you would execute the 001_Core_Schema.sql script here
    -- For now, we'll just check if the schemas exist
    
    IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'audit')
        RAISERROR('Core schema not found. Please run 001_Core_Schema.sql first.', 16, 1);
    
    IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'security')
        RAISERROR('Security schema not found. Please run 001_Core_Schema.sql first.', 16, 1);
    
    IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'system')
        RAISERROR('System schema not found. Please run 001_Core_Schema.sql first.', 16, 1);

    -- Record successful migration
    INSERT INTO migration.MigrationHistory (MigrationId, ScriptName, ExecutionTime)
    VALUES (
        '001_Initial_Setup',
        '001_Initial_Setup.sql',
        DATEDIFF(MILLISECOND, @StartTime, SYSUTCDATETIME())
    );

    COMMIT TRANSACTION;
    PRINT 'Migration 001_Initial_Setup completed successfully.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    SET @ErrorMessage = ERROR_MESSAGE();
    
    -- Record failed migration
    INSERT INTO migration.MigrationHistory (
        MigrationId, 
        ScriptName, 
        ExecutionTime, 
        Success, 
        ErrorMessage
    )
    VALUES (
        '001_Initial_Setup',
        '001_Initial_Setup.sql',
        DATEDIFF(MILLISECOND, @StartTime, SYSUTCDATETIME()),
        0,
        @ErrorMessage
    );

    -- Re-throw the error
    THROW;
END CATCH
GO