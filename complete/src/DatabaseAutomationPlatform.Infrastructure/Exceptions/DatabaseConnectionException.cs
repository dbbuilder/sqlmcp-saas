using System.Runtime.Serialization;

namespace DatabaseAutomationPlatform.Infrastructure.Exceptions
{
    /// <summary>
    /// Exception thrown when database connection operations fail
    /// </summary>
    [Serializable]
    public class DatabaseConnectionException : InfrastructureException
    {
        public string? ConnectionName { get; }
        public string? ServerName { get; }
        public string? DatabaseName { get; }

        public DatabaseConnectionException() : base()
        {
        }

        public DatabaseConnectionException(string message) 
            : base(message, "DB_CONNECTION_ERROR")
        {
        }

        public DatabaseConnectionException(string message, Exception innerException) 
            : base(message, innerException, "DB_CONNECTION_ERROR")
        {
        }

        public DatabaseConnectionException(string message, string connectionName, 
            string serverName, string databaseName) 
            : base(message, "DB_CONNECTION_ERROR")
        {
            ConnectionName = connectionName;
            ServerName = serverName;
            DatabaseName = databaseName;            
            AdditionalData = new Dictionary<string, object>
            {
                ["ConnectionName"] = connectionName ?? "Unknown",
                ["ServerName"] = serverName ?? "Unknown",
                ["DatabaseName"] = databaseName ?? "Unknown"
            };
        }

        protected DatabaseConnectionException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            ConnectionName = info.GetString(nameof(ConnectionName));
            ServerName = info.GetString(nameof(ServerName));
            DatabaseName = info.GetString(nameof(DatabaseName));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ConnectionName), ConnectionName);
            info.AddValue(nameof(ServerName), ServerName);
            info.AddValue(nameof(DatabaseName), DatabaseName);
        }
    }
}