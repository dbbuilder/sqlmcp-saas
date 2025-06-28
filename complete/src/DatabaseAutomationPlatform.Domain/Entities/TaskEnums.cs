namespace DatabaseAutomationPlatform.Domain.Entities
{
    /// <summary>
    /// Types of database automation tasks
    /// </summary>
    public enum DatabaseTaskType
    {
        // SQL Developer Tasks
        SchemaAnalysis,
        QueryOptimization,
        IndexAnalysis,
        StoredProcedureGeneration,
        MigrationGeneration,
        PerformanceTuning,
        CodeGeneration,
        DependencyAnalysis,
        
        // SQL DBA Tasks
        HealthCheck,
        PerformanceMonitoring,
        SecurityAudit,
        BackupValidation,
        MaintenancePlanning,
        UserManagement,
        ComplianceCheck,
        CapacityPlanning,
        
        // Schema Operations
        SchemaComparison,
        SchemaMigration,
        SchemaValidation,
        EnvironmentSync,
        SchemaDocumentation,
        
        // Data Analysis
        DataProfiling,
        DataQualityAssessment,
        AnomalyDetection,
        StatisticalAnalysis,
        SyntheticDataGeneration,
        PIIDetection
    }

    /// <summary>
    /// Risk levels for database tasks
    /// </summary>
    public enum TaskRiskLevel
    {
        /// <summary>
        /// Low risk - read-only operations, no data modification
        /// </summary>
        Low,

        /// <summary>
        /// Medium risk - non-production changes, minor modifications
        /// </summary>
        Medium,

        /// <summary>
        /// High risk - production changes, significant modifications
        /// </summary>
        High,

        /// <summary>
        /// Critical risk - major system changes, potential for data loss
        /// </summary>
        Critical
    }

    /// <summary>
    /// Status of task execution
    /// </summary>
    public enum TaskStatus
    {
        /// <summary>
        /// Task is pending and waiting to be processed
        /// </summary>
        Pending,

        /// <summary>
        /// Task is waiting for approval before execution
        /// </summary>
        WaitingForApproval,

        /// <summary>
        /// Task has been approved and is ready for execution
        /// </summary>
        Approved,

        /// <summary>
        /// Task is currently running
        /// </summary>
        Running,

        /// <summary>
        /// Task has completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Task execution failed
        /// </summary>
        Failed,

        /// <summary>
        /// Task was cancelled by user or system
        /// </summary>
        Cancelled,

        /// <summary>
        /// Task execution timed out
        /// </summary>
        TimedOut
    }

    /// <summary>
    /// Approval status for tasks requiring approval
    /// </summary>
    public enum ApprovalStatus
    {
        /// <summary>
        /// No approval required for this task
        /// </summary>
        NotRequired,

        /// <summary>
        /// Approval is pending
        /// </summary>
        Pending,

        /// <summary>
        /// Task has been approved
        /// </summary>
        Approved,

        /// <summary>
        /// Task approval was rejected
        /// </summary>
        Rejected,

        /// <summary>
        /// Approval request has expired
        /// </summary>
        Expired
    }
}
