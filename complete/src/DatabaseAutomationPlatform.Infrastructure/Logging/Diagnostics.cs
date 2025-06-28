using System.Diagnostics;

namespace DatabaseAutomationPlatform.Infrastructure.Logging
{
    /// <summary>
    /// Provides diagnostic instrumentation for the infrastructure layer
    /// </summary>
    public static class Diagnostics
    {
        /// <summary>
        /// Name of the activity source
        /// </summary>
        public const string ActivitySourceName = "DatabaseAutomationPlatform.Infrastructure";
        
        /// <summary>
        /// Version of the activity source
        /// </summary>
        public const string ActivitySourceVersion = "1.0.0";
        
        /// <summary>
        /// Activity source for distributed tracing
        /// </summary>
        public static readonly ActivitySource ActivitySource = new(ActivitySourceName, ActivitySourceVersion);
    }
}