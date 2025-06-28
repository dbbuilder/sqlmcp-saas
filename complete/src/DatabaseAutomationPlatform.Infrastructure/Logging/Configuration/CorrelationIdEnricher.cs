using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace DatabaseAutomationPlatform.Infrastructure.Logging.Configuration
{
    /// <summary>
    /// Enriches log events with correlation ID for request tracking
    /// </summary>
    public class CorrelationIdEnricher : ILogEventEnricher
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string CorrelationIdPropertyName = "CorrelationId";
        private const string CorrelationIdHeaderName = "X-Correlation-ID";

        /// <summary>
        /// Initializes a new instance of the CorrelationIdEnricher class
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor</param>
        public CorrelationIdEnricher(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        /// <summary>
        /// Enriches the log event with correlation ID
        /// </summary>
        /// <param name="logEvent">The log event to enrich</param>
        /// <param name="propertyFactory">Property factory for creating log event properties</param>
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            if (propertyFactory == null) throw new ArgumentNullException(nameof(propertyFactory));

            var correlationId = GetCorrelationId();
            var property = propertyFactory.CreateProperty(CorrelationIdPropertyName, correlationId);
            logEvent.AddPropertyIfAbsent(property);
        }

        /// <summary>
        /// Gets the correlation ID from the current context
        /// </summary>
        /// <returns>The correlation ID</returns>
        private string GetCorrelationId()
        {
            var context = _httpContextAccessor.HttpContext;

            if (context != null)
            {
                // Try to get from the items collection first (might be set by middleware)
                if (context.Items.TryGetValue(CorrelationIdPropertyName, out var itemsCorrelationId) && 
                    itemsCorrelationId is string correlationIdFromItems)
                {
                    return correlationIdFromItems;
                }

                // Try to get from request headers
                if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var headerValues))
                {
                    var correlationId = headerValues.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(correlationId))
                    {
                        return correlationId;
                    }
                }

                // Try to get from TraceIdentifier
                if (!string.IsNullOrWhiteSpace(context.TraceIdentifier))
                {
                    return context.TraceIdentifier;
                }
            }

            // Fallback to a new GUID if no correlation ID is found
            return Guid.NewGuid().ToString();
        }
    }
}