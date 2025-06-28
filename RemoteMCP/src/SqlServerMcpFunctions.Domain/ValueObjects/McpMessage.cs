using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SqlServerMcpFunctions.Domain.ValueObjects
{
    /// <summary>
    /// Base class for all MCP protocol messages
    /// </summary>
    public abstract class McpMessage
    {
        /// <summary>
        /// MCP protocol version
        /// </summary>
        public string JsonRpc { get; set; } = "2.0";

        /// <summary>
        /// Unique identifier for this message
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Message type identifier
        /// </summary>
        public abstract string Method { get; }

        /// <summary>
        /// Timestamp when the message was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// MCP request message
    /// </summary>
    /// <typeparam name="T">Type of the request parameters</typeparam>
    public class McpRequest<T> : McpMessage where T : class
    {
        public override string Method { get; }
        
        /// <summary>
        /// Request parameters
        /// </summary>
        public T? Params { get; set; }

        public McpRequest(string method)
        {
            Method = method;
            Id = Guid.NewGuid().ToString();
        }
    }

    /// <summary>
    /// MCP response message
    /// </summary>
    /// <typeparam name="T">Type of the response result</typeparam>
    public class McpResponse<T> : McpMessage
    {
        public override string Method => string.Empty;

        /// <summary>
        /// Response result data
        /// </summary>
        public T? Result { get; set; }

        /// <summary>
        /// Error information if the request failed
        /// </summary>
        public McpError? Error { get; set; }

        /// <summary>
        /// Whether the response indicates success
        /// </summary>
        public bool IsSuccess => Error == null;
    }

    /// <summary>
    /// MCP error information
    /// </summary>
    public class McpError
    {
        /// <summary>
        /// Error code
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Additional error data
        /// </summary>
        public JsonDocument? Data { get; set; }
    }
}
