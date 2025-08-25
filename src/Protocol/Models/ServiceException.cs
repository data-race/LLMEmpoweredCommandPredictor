using System.Runtime.Serialization;

namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Custom exception for service communication errors.
/// </summary>
public class ServiceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the ServiceException class.
    /// </summary>
    /// <param name="message">Error message</param>
    public ServiceException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the ServiceException class.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="innerException">Inner exception</param>
    public ServiceException(string message, Exception innerException) : base(message, innerException) { }
}
