namespace DatabaseAutomationPlatform.Infrastructure.Configuration
{
    /// <summary>
    /// Database connection configuration options
    /// </summary>
    public class DatabaseOptions
    {
        /// <summary>
        /// Configuration section name
        /// </summary>
        public const string SectionName = "Database";
        
        /// <summary>
        /// Connection timeout in seconds (default: 30)
        /// </summary>
        public int ConnectionTimeout { get; set; } = 30;
        
        /// <summary>
        /// Command timeout in seconds (default: 30)
        /// </summary>
        public int CommandTimeout { get; set; } = 30;
        
        /// <summary>
        /// Maximum retry attempts for transient failures (default: 3)
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;
        
        /// <summary>
        /// Delay between retries in milliseconds (default: 1000)
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 1000;
        
        /// <summary>
        /// Enable connection pooling (default: true)
        /// </summary>
        public bool EnableConnectionPooling { get; set; } = true;
        
        /// <summary>
        /// Minimum pool size (default: 5)
        /// </summary>
        public int MinPoolSize { get; set; } = 5;
        
        /// <summary>
        /// Maximum pool size (default: 100)
        /// </summary>
        public int MaxPoolSize { get; set; } = 100;
        
        /// <summary>
        /// Validate configuration options
        /// </summary>
        public void Validate()
        {
            if (ConnectionTimeout <= 0)
                throw new ArgumentException("ConnectionTimeout must be greater than 0");
                
            if (CommandTimeout <= 0)
                throw new ArgumentException("CommandTimeout must be greater than 0");
                
            if (MaxRetryAttempts < 0)
                throw new ArgumentException("MaxRetryAttempts cannot be negative");
                
            if (RetryDelayMilliseconds < 0)
                throw new ArgumentException("RetryDelayMilliseconds cannot be negative");
                
            if (MinPoolSize < 0)
                throw new ArgumentException("MinPoolSize cannot be negative");
                
            if (MaxPoolSize < MinPoolSize)
                throw new ArgumentException("MaxPoolSize must be greater than or equal to MinPoolSize");
        }
    }
}