using LLMEmpoweredCommandPredictor.Protocol.Client;
using LLMEmpoweredCommandPredictor.Protocol.Contracts;
using LLMEmpoweredCommandPredictor.Protocol.Models;
using LLMEmpoweredCommandPredictor.Protocol.Server;

namespace LLMEmpoweredCommandPredictor.Protocol.Factory;

/// <summary>
/// Factory class for creating client and server instances with pre-configured settings.
/// </summary>
public static class ProtocolFactory
{
    /// <summary>
    /// Default connection settings optimized for production use.
    /// </summary>
    public static readonly ConnectionSettings DefaultSettings = new()
    {
        TimeoutMs = 15,
        ConnectionTimeoutMs = 1000,
        MaxRetries = 3,
        RetryDelayMs = 100,
        EnableDebugLogging = false
    };

    /// <summary>
    /// Creates a new suggestion service client with default settings.
    /// </summary>
    /// <returns>Configured suggestion service client</returns>
    public static SuggestionServiceClient CreateClient()
    {
        return CreateClient(DefaultSettings);
    }

    /// <summary>
    /// Creates a new suggestion service client with custom settings.
    /// </summary>
    /// <param name="settings">Connection settings</param>
    /// <returns>Configured suggestion service client</returns>
    public static SuggestionServiceClient CreateClient(ConnectionSettings settings)
    {
        return new SuggestionServiceClient(settings);
    }

    /// <summary>
    /// Creates a new suggestion service client with debug logging enabled.
    /// </summary>
    /// <returns>Configured suggestion service client with debug logging</returns>
    public static SuggestionServiceClient CreateDebugClient()
    {
        var debugSettings = new ConnectionSettings
        {
            TimeoutMs = DefaultSettings.TimeoutMs,
            ConnectionTimeoutMs = DefaultSettings.ConnectionTimeoutMs,
            MaxRetries = DefaultSettings.MaxRetries,
            RetryDelayMs = DefaultSettings.RetryDelayMs,
            EnableDebugLogging = true
        };
        return CreateClient(debugSettings);
    }

    /// <summary>
    /// Creates a new suggestion service server with default configuration.
    /// </summary>
    /// <param name="service">The suggestion service implementation</param>
    /// <returns>Configured suggestion service server</returns>
    public static SuggestionServiceServer CreateServer(ISuggestionService service)
    {
        return CreateServer(service, "LLMEmpoweredCommandPredictor.SuggestionService");
    }

    /// <summary>
    /// Creates a new suggestion service server with custom pipe name.
    /// </summary>
    /// <param name="service">The suggestion service implementation</param>
    /// <param name="pipeName">Named pipe name for communication</param>
    /// <returns>Configured suggestion service server</returns>
    public static SuggestionServiceServer CreateServer(
        ISuggestionService service,
        string pipeName)
    {
        return new SuggestionServiceServer(service, pipeName);
    }
}
