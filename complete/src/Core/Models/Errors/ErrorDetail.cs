namespace SqlMcp.Core.Models.Errors
{
    /// <summary>
    /// Represents a detailed error with code, message, and optional field information.
    /// </summary>
    public class ErrorDetail
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the field that caused the error (optional).
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// Initializes a new instance of the ErrorDetail class.
        /// </summary>
        public ErrorDetail()
        {
        }

        /// <summary>
        /// Initializes a new instance of the ErrorDetail class with specified values.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="field">The field that caused the error.</param>
        public ErrorDetail(string code, string message, string field = null)
        {
            Code = code;
            Message = message;
            Field = field;
        }
    }
}
