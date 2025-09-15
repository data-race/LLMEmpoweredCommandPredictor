using LLMEmpoweredCommandPredictor.Protocol.Client;
using LLMEmpoweredCommandPredictor.Protocol.Contracts;
using LLMEmpoweredCommandPredictor.Protocol.Models;
using LLMEmpoweredCommandPredictor.Protocol.Server;

namespace LLMEmpoweredCommandPredictor.Protocol.Factory;

/// <summary>
/// Factory for creating Protocol client and server instances with optimized configurations.
/// Provides preset configurations for different use cases and environments.
/// </summary>
public static class ProtocolFactory
{
    /// <summary>
    /// Default connection settings optimized for general use
    /// </summary>
    public static ConnectionSettings DefaultSettings => new()
    {
        TimeoutMs = 15,
        ConnectionTimeoutMs = 1000,
        MaxRetries = 3,
        RetryDelayMs = 100,
        EnableDebugLogging = true // Enable debug logging to see connection attempts
    };


    /// <summary>
    /// Creates a client with default settings
    /// </summary>
    public static SuggestionServiceClient CreateClient()
    {
        return new SuggestionServiceClient(DefaultSettings);
    }

    /// <summary>
    /// Creates a client with custom settings
    /// </summary>
    public static SuggestionServiceClient CreateClient(ConnectionSettings settings)
    {
        return new SuggestionServiceClient(settings);
    }


    /// <summary>
    /// Creates a server with the specified service implementation
    /// </summary>
    public static SuggestionServiceServer CreateServer(ISuggestionService service, string pipeName)
    {
        return new SuggestionServiceServer(service, pipeName);
    }

    /// <summary>
    /// Creates a server with default pipe name
    /// </summary>
    public static SuggestionServiceServer CreateServer(ISuggestionService service)
    {
        return new SuggestionServiceServer(service, "LLMEmpoweredCommandPredictor.SuggestionService");
    }


}
