using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace SqlServerMcpFunctions.Functions
{
    /// <summary>
    /// Helper methods for McpServerFunctions
    /// </summary>
    public partial class McpServerFunctions
    {
        /// <summary>
        /// Read and deserialize request body
        /// </summary>
        private async Task<T?> ReadRequestBodyAsync<T>(HttpRequestData req) where T : class
        {
            try
            {
                var body = await req.ReadAsStringAsync();
                
                if (string.IsNullOrEmpty(body))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<T>(body, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize request body");
                return null;
            }
        }

        /// <summary>
        /// Extract request ID from request headers or body
        /// </summary>
        private string ExtractRequestId(HttpRequestData req)
        {
            // Try to get from headers first
            if (req.Headers.TryGetValues("X-Request-ID", out var values))
            {
                return values.FirstOrDefault() ?? Guid.NewGuid().ToString();
            }

            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Create standardized error response
        /// </summary>
        private async Task<HttpResponseData> CreateErrorResponseAsync(
            HttpRequestData req, 
            string correlationId, 
            int errorCode, 
            string errorMessage)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            response.Headers.Add("Content-Type", "application/json");
            response.Headers.Add("X-Correlation-ID", correlationId);

            var errorResult = new
            {
                jsonrpc = "2.0",
                id = ExtractRequestId(req),
                error = new
                {
                    code = errorCode,
                    message = errorMessage,
                    data = new
                    {
                        correlationId = correlationId,
                        timestamp = DateTime.UtcNow
                    }
                }
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(errorResult, _jsonOptions));
            return response;
        }
    }
}
