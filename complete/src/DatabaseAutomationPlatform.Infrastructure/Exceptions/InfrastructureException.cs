using System.Runtime.Serialization;

namespace DatabaseAutomationPlatform.Infrastructure.Exceptions
{
    /// <summary>
    /// Base exception for all infrastructure-related exceptions
    /// </summary>
    [Serializable]
    public class InfrastructureException : Exception
    {
        public string? ErrorCode { get; }
        public Dictionary<string, object>? AdditionalData { get; }

        public InfrastructureException() : base()
        {
        }

        public InfrastructureException(string message) : base(message)
        {
        }

        public InfrastructureException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        public InfrastructureException(string message, string errorCode) 
            : base(message)
        {
            ErrorCode = errorCode;
        }
        public InfrastructureException(string message, string errorCode, 
            Dictionary<string, object> additionalData) 
            : base(message)
        {
            ErrorCode = errorCode;
            AdditionalData = additionalData;
        }

        public InfrastructureException(string message, Exception innerException, 
            string errorCode) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        protected InfrastructureException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            ErrorCode = info.GetString(nameof(ErrorCode));
            AdditionalData = info.GetValue(nameof(AdditionalData), 
                typeof(Dictionary<string, object>)) as Dictionary<string, object>;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ErrorCode), ErrorCode);
            info.AddValue(nameof(AdditionalData), AdditionalData);
        }
    }
}