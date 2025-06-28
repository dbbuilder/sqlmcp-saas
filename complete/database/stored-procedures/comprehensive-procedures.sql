-- =====================================================
-- Database Automation Platform - Complete Stored Procedures
-- =====================================================
-- This file contains all stored procedures for the comprehensive
-- Database Automation Platform covering SQL Developer, DBA,
-- Schema Management, and Data Analytics operations.
-- =====================================================

-- SQL DEVELOPER PROCEDURES
-- =====================================================

-- Comprehensive Schema Analysis
CREATE OR ALTER PROCEDURE sp_AnalyzeTableSchema
    @DatabaseName NVARCHAR(128),
    @TableName NVARCHAR(128) = NULL,
    @IncludeConstraints BIT = 1,
    @IncludeIndexes BIT = 1,
    @IncludeTriggers BIT = 1,
    @IncludeStatistics BIT = 1
AS
BEGIN
    SET NOCOUNT ON
    
    -- Table and column information
    SELECT 
        s.name as SchemaName,
        t.name as TableName,
        c.name as ColumnName,
        ty.name as DataType,
        c.max_length,
        c.precision,
        c.scale,
        c.is_nullable,
        c.is_identity,
        c.is_computed,
        dc.definition as DefaultValue,
        CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END as IsPrimaryKey,
        CASE WHEN fk.parent_column_id IS NOT NULL THEN 1 ELSE 0 END as IsForeignKey,
        rt.name as ReferencedTable,
        rc.name as ReferencedColumn,
        ep.value as ColumnDescription,
        t.create_date as TableCreated,
        t.modify_date as TableModified
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    INNER JOIN sys.columns c ON t.object_id = c.object_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
    LEFT JOIN sys.index_columns pk ON t.object_id = pk.object_id 
        AND c.column_id = pk.column_id 
        AND pk.index_id = 1
    LEFT JOIN sys.foreign_key_columns fk ON t.object_id = fk.parent_object_id 
        AND c.column_id = fk.parent_column_id
    LEFT JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
    LEFT JOIN sys.columns rc ON fk.referenced_object_id = rc.object_id 
        AND fk.referenced_column_id = rc.column_id
    LEFT JOIN sys.extended_properties ep ON t.object_id = ep.major_id 
        AND c.column_id = ep.minor_id 
        AND ep.name = 'MS_Description'
    WHERE (@TableName IS NULL OR t.name = @TableName)
    ORDER BY s.name, t.name, c.column_id

    -- Index analysis (if requested)
    IF @IncludeIndexes = 1
    BEGIN
        SELECT 
            s.name as SchemaName,
            t.name as TableName,
            i.name as IndexName,
            i.type_desc as IndexType,
            i.is_primary_key,
            i.is_unique,
            i.is_disabled,
            ps.avg_fragmentation_in_percent,
            ps.page_count,
            ius.user_seeks,
            ius.user_scans,
            ius.user_lookups,
            ius.user_updates,
            STUFF((
                SELECT ', ' + c.name + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE ' ASC' END
                FROM sys.index_columns ic
                INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                ORDER BY ic.key_ordinal
                FOR XML PATH('')
            ), 1, 2, '') as KeyColumns,
            STUFF((
                SELECT ', ' + c.name
                FROM sys.index_columns ic
                INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
                ORDER BY ic.key_ordinal
                FOR XML PATH('')
            ), 1, 2, '') as IncludedColumns
        FROM sys.indexes i
        INNER JOIN sys.tables t ON i.object_id = t.object_id
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        LEFT JOIN sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'SAMPLED') ps 
            ON i.object_id = ps.object_id AND i.index_id = ps.index_id
        LEFT JOIN sys.dm_db_index_usage_stats ius 
            ON i.object_id = ius.object_id AND i.index_id = ius.index_id
        WHERE (@TableName IS NULL OR t.name = @TableName)
            AND i.index_id > 0
        ORDER BY s.name, t.name, i.name
    END

    -- Constraint information (if requested)
    IF @IncludeConstraints = 1
    BEGIN
        SELECT 
            s.name as SchemaName,
            t.name as TableName,
            con.name as ConstraintName,
            con.type_desc as ConstraintType,
            col.name as ColumnName,
            con.definition as ConstraintDefinition,
            con.is_disabled,
            con.is_not_trusted
        FROM sys.check_constraints con
        INNER JOIN sys.tables t ON con.parent_object_id = t.object_id
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        LEFT JOIN sys.columns col ON con.parent_object_id = col.object_id 
            AND con.parent_column_id = col.column_id
        WHERE (@TableName IS NULL OR t.name = @TableName)
        
        UNION ALL
        
        SELECT 
            s.name as SchemaName,
            pt.name as TableName,
            fk.name as ConstraintName,
            'FOREIGN KEY' as ConstraintType,
            pc.name as ColumnName,
            rs.name + '.' + rt.name + '(' + rc.name + ')' as ConstraintDefinition,
            fk.is_disabled,
            fk.is_not_trusted
        FROM sys.foreign_keys fk
        INNER JOIN sys.tables pt ON fk.parent_object_id = pt.object_id
        INNER JOIN sys.schemas s ON pt.schema_id = s.schema_id
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        INNER JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
        INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
        INNER JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
        INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
        WHERE (@TableName IS NULL OR pt.name = @TableName)
        
        ORDER BY SchemaName, TableName, ConstraintName
    END
END

-- Query Performance Analysis
CREATE OR ALTER PROCEDURE sp_AnalyzeQueryPerformance
    @Query NVARCHAR(MAX),
    @DatabaseName NVARCHAR(128),
    @AnalysisType VARCHAR(20) = 'FULL', -- PLAN, STATS, FULL
    @GenerateOptimizations BIT = 1
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @SQL NVARCHAR(MAX)
    DECLARE @PlanXML XML
    
    CREATE TABLE #QueryAnalysis (
        AnalysisType VARCHAR(50),
        Metric VARCHAR(100),
        Value VARCHAR(MAX),
        Recommendation VARCHAR(MAX),
        Priority INT
    )
    
    -- Basic query validation
    INSERT INTO #QueryAnalysis VALUES ('Validation', 'QueryLength', CAST(LEN(@Query) as VARCHAR), 
        CASE WHEN LEN(@Query) > 4000 THEN 'Consider breaking down complex query' ELSE 'OK' END, 1)
    
    -- Analyze query structure
    INSERT INTO #QueryAnalysis VALUES ('Structure', 'SelectStatements', 
        CAST((LEN(@Query) - LEN(REPLACE(UPPER(@Query), 'SELECT', ''))) / 6 as VARCHAR),
        CASE WHEN (LEN(@Query) - LEN(REPLACE(UPPER(@Query), 'SELECT', ''))) / 6 > 5 
             THEN 'Consider simplifying complex queries' ELSE 'OK' END, 2)
    
    -- Check for common anti-patterns
    IF @Query LIKE '%SELECT *%'
        INSERT INTO #QueryAnalysis VALUES ('AntiPattern', 'SelectStar', 'Found', 
            'Avoid SELECT * - specify only needed columns', 3)
    
    IF @Query LIKE '%NOLOCK%' OR @Query LIKE '%READUNCOMMITTED%'
        INSERT INTO #QueryAnalysis VALUES ('AntiPattern', 'NoLock', 'Found', 
            'NOLOCK can cause data inconsistency - consider READ COMMITTED SNAPSHOT', 3)
    
    IF @Query LIKE '%CURSOR%'
        INSERT INTO #QueryAnalysis VALUES ('AntiPattern', 'Cursor', 'Found', 
            'Consider set-based operations instead of cursors', 2)
    
    -- Performance recommendations based on query patterns
    IF @GenerateOptimizations = 1
    BEGIN
        -- Look for missing WHERE clauses
        IF @Query LIKE '%FROM%' AND @Query NOT LIKE '%WHERE%' AND @Query NOT LIKE '%JOIN%'
            INSERT INTO #QueryAnalysis VALUES ('Optimization', 'MissingFilter', 'Potential table scan', 
                'Add WHERE clause to filter results', 1)
        
        -- Check for functions in WHERE clause
        IF @Query LIKE '%WHERE%UPPER(%' OR @Query LIKE '%WHERE%LOWER(%' OR @Query LIKE '%WHERE%SUBSTRING%'
            INSERT INTO #QueryAnalysis VALUES ('Optimization', 'FunctionInWhere', 'Non-SARGable predicate', 
                'Avoid functions in WHERE clause - consider computed columns or rewrite', 2)
        
        -- Check for OR conditions
        IF @Query LIKE '%WHERE%OR%'
            INSERT INTO #QueryAnalysis VALUES ('Optimization', 'OrCondition', 'Potential index issues', 
                'Consider UNION ALL instead of OR for better index usage', 2)
        
        -- Check for implicit conversions
        IF @Query LIKE '%=%''%' 
            INSERT INTO #QueryAnalysis VALUES ('Optimization', 'ImplicitConversion', 'Potential type mismatch', 
                'Ensure data types match to avoid implicit conversions', 2)
    END
    
    -- Return analysis results
    SELECT * FROM #QueryAnalysis ORDER BY Priority, AnalysisType, Metric
    
    DROP TABLE #QueryAnalysis
END

-- SQL DBA PROCEDURES  
-- =====================================================

-- Comprehensive Database Health Check
CREATE OR ALTER PROCEDURE sp_DatabaseHealthCheck
    @DatabaseName NVARCHAR(128) = NULL,
    @IncludePerformance BIT = 1,
    @IncludeSecurity BIT = 1,
    @IncludeBackups BIT = 1,
    @IncludeMaintenance BIT = 1
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @HealthScore INT = 100
    DECLARE @IssueCount INT = 0
    
    CREATE TABLE #HealthIssues (
        Category VARCHAR(50),
        Issue VARCHAR(200),
        Severity VARCHAR(20),
        Impact INT,
        Recommendation VARCHAR(500),
        DatabaseName NVARCHAR(128)
    )
    
    -- Configuration Health Checks
    INSERT INTO #HealthIssues
    SELECT 
        'Configuration',
        'AUTO_CLOSE is enabled on database: ' + name,
        'Medium',
        10,
        'Disable AUTO_CLOSE for better performance: ALTER DATABASE [' + name + '] SET AUTO_CLOSE OFF',
        name
    FROM sys.databases 
    WHERE is_auto_close_on = 1 
        AND (@DatabaseName IS NULL OR name = @DatabaseName)
        AND database_id > 4
    
    INSERT INTO #HealthIssues
    SELECT 
        'Configuration',
        'AUTO_SHRINK is enabled on database: ' + name,
        'High',
        15,
        'Disable AUTO_SHRINK to prevent performance issues: ALTER DATABASE [' + name + '] SET AUTO_SHRINK OFF',
        name
    FROM sys.databases 
    WHERE is_auto_shrink_on = 1 
        AND (@DatabaseName IS NULL OR name = @DatabaseName)
        AND database_id > 4
    
    -- Performance Health Checks
    IF @IncludePerformance = 1
    BEGIN
        -- Check for tables without clustered indexes (heaps)
        INSERT INTO #HealthIssues
        SELECT 
            'Performance',
            'Table ' + s.name + '.' + t.name + ' is a heap (no clustered index)',
            'Medium',
            8,
            'Consider adding a clustered index for better performance',
            DB_NAME()
        FROM sys.tables t
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE NOT EXISTS (
            SELECT 1 FROM sys.indexes i 
            WHERE i.object_id = t.object_id AND i.type = 1
        )
        AND t.type = 'U'
        
        -- Check for high fragmentation indexes
        INSERT INTO #HealthIssues
        SELECT 
            'Performance',
            'Index ' + i.name + ' on ' + s.name + '.' + t.name + ' is ' + 
            CAST(CAST(ps.avg_fragmentation_in_percent as INT) as VARCHAR) + '% fragmented',
            CASE WHEN ps.avg_fragmentation_in_percent > 30 THEN 'High' ELSE 'Medium' END,
            CASE WHEN ps.avg_fragmentation_in_percent > 30 THEN 12 ELSE 6 END,
            'Rebuild index: ALTER INDEX [' + i.name + '] ON [' + s.name + '].[' + t.name + '] REBUILD',
            DB_NAME()
        FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'SAMPLED') ps
        INNER JOIN sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
        INNER JOIN sys.tables t ON i.object_id = t.object_id
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE ps.avg_fragmentation_in_percent > 15
            AND i.index_id > 0
            AND ps.page_count > 1000
    END
    
    -- Security Health Checks
    IF @IncludeSecurity = 1
    BEGIN
        -- Check for users with excessive privileges
        INSERT INTO #HealthIssues
        SELECT 
            'Security',
            'Login ' + sp.name + ' has sysadmin privileges',
            'Critical',
            25,
            'Review if sysadmin privileges are necessary. Consider principle of least privilege.',
            'Server'
        FROM sys.server_role_members srm
        INNER JOIN sys.server_principals sp ON srm.member_principal_id = sp.principal_id
        WHERE srm.role_principal_id = (SELECT principal_id FROM sys.server_principals WHERE name = 'sysadmin')
            AND sp.name NOT IN ('sa', 'NT AUTHORITY\SYSTEM', 'NT SERVICE\MSSQLSERVER', '##MS_PolicyEventProcessingLogin##')
            AND sp.type IN ('S', 'U')
        
        -- Check for weak password policies
        INSERT INTO #HealthIssues
        SELECT 
            'Security',
            'SQL Login ' + name + ' has weak password policy settings',
            'Medium',
            10,
            'Enable password policy checking: ALTER LOGIN [' + name + '] WITH CHECK_POLICY = ON',
            'Server'
        FROM sys.sql_logins 
        WHERE (is_policy_checked = 0 OR is_expiration_checked = 0)
            AND name NOT LIKE '##%##'
    END
    
    -- Backup Health Checks
    IF @IncludeBackups = 1
    BEGIN
        -- Check for databases without recent backups
        INSERT INTO #HealthIssues
        SELECT 
            'Backup',
            'Database ' + d.name + ' has not been backed up in the last 7 days',
            'Critical',
            30,
            'Implement regular backup schedule. Last backup: ' + 
                ISNULL(CAST(b.last_backup as VARCHAR), 'Never'),
            d.name
        FROM sys.databases d
        LEFT JOIN (
            SELECT 
                database_name,
                MAX(backup_finish_date) as last_backup
            FROM msdb.dbo.backupset 
            WHERE type = 'D'
            GROUP BY database_name
        ) b ON d.name = b.database_name
        WHERE d.database_id > 4
            AND (b.last_backup IS NULL OR b.last_backup < DATEADD(day, -7, GETDATE()))
            AND (@DatabaseName IS NULL OR d.name = @DatabaseName)
            AND d.state = 0 -- ONLINE
    END
    
    -- Maintenance Health Checks
    IF @IncludeMaintenance = 1
    BEGIN
        -- Check for outdated statistics
        INSERT INTO #HealthIssues
        SELECT 
            'Maintenance',
            'Statistics on ' + s.name + '.' + t.name + ' are outdated (last updated: ' + 
                ISNULL(CAST(sp.last_updated as VARCHAR), 'Unknown') + ')',
            'Medium',
            5,
            'Update statistics: UPDATE STATISTICS [' + s.name + '].[' + t.name + ']',
            DB_NAME()
        FROM sys.stats st
        INNER JOIN sys.tables t ON st.object_id = t.object_id
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        CROSS APPLY sys.dm_db_stats_properties(st.object_id, st.stats_id) sp
        WHERE (sp.last_updated < DATEADD(day, -7, GETDATE()) OR sp.last_updated IS NULL)
            AND st.auto_created = 0
    END
    
    -- Calculate health score
    SELECT @IssueCount = COUNT(*), @HealthScore = @HealthScore - SUM(Impact) FROM #HealthIssues
    SET @HealthScore = CASE WHEN @HealthScore < 0 THEN 0 ELSE @HealthScore END
    
    -- Return health summary
    SELECT 
        @HealthScore as OverallHealthScore,
        CASE 
            WHEN @HealthScore >= 90 THEN 'Excellent'
            WHEN @HealthScore >= 75 THEN 'Good'
            WHEN @HealthScore >= 60 THEN 'Fair'
            WHEN @HealthScore >= 40 THEN 'Poor'
            ELSE 'Critical'
        END as HealthStatus,
        @IssueCount as TotalIssues,
        SUM(CASE WHEN Severity = 'Critical' THEN 1 ELSE 0 END) as CriticalIssues,
        SUM(CASE WHEN Severity = 'High' THEN 1 ELSE 0 END) as HighSeverityIssues,
        SUM(CASE WHEN Severity = 'Medium' THEN 1 ELSE 0 END) as MediumSeverityIssues,
        SUM(CASE WHEN Severity = 'Low' THEN 1 ELSE 0 END) as LowSeverityIssues,
        GETDATE() as AssessmentDate
    FROM #HealthIssues
    
    -- Return detailed issues
    SELECT 
        Category,
        Issue,
        Severity,
        Impact,
        Recommendation,
        DatabaseName
    FROM #HealthIssues 
    ORDER BY 
        CASE Severity 
            WHEN 'Critical' THEN 1 
            WHEN 'High' THEN 2 
            WHEN 'Medium' THEN 3 
            WHEN 'Low' THEN 4 
        END, 
        Impact DESC,
        Category
    
    DROP TABLE #HealthIssues
END

-- Continue with more procedures in next chunk...
