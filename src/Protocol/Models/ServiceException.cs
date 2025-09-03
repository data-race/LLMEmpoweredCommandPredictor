using System;

namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Custom exception for service communication errors.
/// Provides detailed error information for debugging and error handling.
/// </summary>
public class ServiceException : Exception
{
    /// <summary>
    /// Type of service error that occurred
    /// </summary>
    public ServiceErrorType ErrorType { get; }

    /// <summary>
    /// Whether this error is recoverable (can be retried)
    /// </summary>
    public bool IsRecoverable { get; }

    /// <summary>
    /// Timestamp when the error occurred
    /// </summary>
    public DateTime ErrorTimestamp { get; }

    /// <summary>
    /// Additional context information about the error
    /// </summary>
    public string? ErrorContext { get; }

    /// <summary>
    /// Error code for programmatic error handling
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Creates a new service exception with basic information
    /// </summary>
    public ServiceException(string message) : base(message)
    {
        ErrorType = ServiceErrorType.Unknown;
        IsRecoverable = false;
        ErrorTimestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new service exception with inner exception
    /// </summary>
    public ServiceException(string message, Exception innerException) : base(message, innerException)
    {
        ErrorType = ServiceErrorType.Unknown;
        IsRecoverable = false;
        ErrorTimestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new service exception with detailed information
    /// </summary>
    public ServiceException(
        string message, 
        ServiceErrorType errorType, 
        bool isRecoverable = false, 
        string? errorContext = null,
        string? errorCode = null,
        Exception? innerException = null) : base(message, innerException)
    {
        ErrorType = errorType;
        IsRecoverable = isRecoverable;
        ErrorTimestamp = DateTime.UtcNow;
        ErrorContext = errorContext;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Types of service errors that can occur
/// </summary>
public enum ServiceErrorType
{
    /// <summary>
    /// Unknown or unspecified error
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Connection-related error (network, timeout, etc.)
    /// </summary>
    Connection = 1,

    /// <summary>
    /// Authentication or authorization error
    /// </summary>
    Authentication = 2,

    /// <summary>
    /// Service unavailable or overloaded
    /// </summary>
    ServiceUnavailable = 3,

    /// <summary>
    /// Invalid request or parameters
    /// </summary>
    InvalidRequest = 4,

    /// <summary>
    /// Internal service error
    /// </summary>
    InternalError = 5,

    /// <summary>
    /// Rate limiting or throttling error
    /// </summary>
    RateLimited = 6,

    /// <summary>
    /// Timeout error
    /// </summary>
    Timeout = 7,

    /// <summary>
    /// Resource not found error
    /// </summary>
    NotFound = 8
}
