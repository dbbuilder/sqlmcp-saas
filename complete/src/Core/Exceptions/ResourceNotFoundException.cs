using System;

namespace SqlMcp.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a requested resource is not found.
    /// Maps to HTTP 404 Not Found status.
    /// </summary>
    public class ResourceNotFoundException : BaseException
    {
        /// <summary>
        /// Gets the type of resource that was not found.
        /// </summary>
        public string ResourceType { get; }

        /// <summary>
        /// Gets the identifier of the resource that was not found.
        /// </summary>
        public string ResourceId { get; }

        /// <summary>
        /// Initializes a new instance of the ResourceNotFoundException class.
        /// </summary>
        /// <param name="resourceType">The type of resource.</param>
        /// <param name="resourceId">The resource identifier.</param>
        public ResourceNotFoundException(string resourceType, string resourceId) 
            : base(
                $"{resourceType} with ID '{resourceId}' was not found", 
                $"{resourceType} not found")
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
            AddDetail("ResourceType", resourceType);
            AddDetail("ResourceId", resourceId);
        }

        /// <summary>
        /// Initializes a new instance of the ResourceNotFoundException class with a custom message.
        /// </summary>
        /// <param name="resourceType">The type of resource.</param>
        /// <param name="resourceId">The resource identifier.</param>
        /// <param name="message">The detailed error message.</param>
        public ResourceNotFoundException(string resourceType, string resourceId, string message) 
            : base(message, $"{resourceType} not found")
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
            AddDetail("ResourceType", resourceType);
            AddDetail("ResourceId", resourceId);
        }
    }
}
