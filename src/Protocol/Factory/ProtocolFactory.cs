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
        EnableConnectionPooling = false,
        MaxPoolSize = 5,
        EnableAutoReconnect = false,
        EnableDebugLogging = false
    };

    /// <summary>
    /// High-performance settings for production environments
    /// </summary>
    public static ConnectionSettings HighPerformanceSettings => new()
    {
        TimeoutMs = 10,
        ConnectionTimeoutMs = 500,
        MaxRetries = 2,
        RetryDelayMs = 50,
        EnableConnectionPooling = true,
        MaxPoolSize = 10,
        EnableAutoReconnect = true,
        EnableDebugLogging = false
    };

    /// <summary>
    /// Development settings with detailed logging and relaxed timeouts
    /// </summary>
    public static ConnectionSettings DevelopmentSettings => new()
    {
        TimeoutMs = 30,
        ConnectionTimeoutMs = 2000,
        MaxRetries = 5,
        RetryDelayMs = 200,
        EnableConnectionPooling = false,
        MaxPoolSize = 3,
        EnableAutoReconnect = false,
        EnableDebugLogging = true
    };

    /// <summary>
    /// Reliable settings for critical operations with high retry counts
    /// </summary>
    public static ConnectionSettings ReliableSettings => new()
    {
        TimeoutMs = 20,
        ConnectionTimeoutMs = 1500,
        MaxRetries = 5,
        RetryDelayMs = 150,
        EnableConnectionPooling = true,
        MaxPoolSize = 8,
        EnableAutoReconnect = true,
        EnableDebugLogging = false
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
    /// Creates a client optimized for high performance
    /// </summary>
    public static SuggestionServiceClient CreateHighPerformanceClient()
    {
        return new SuggestionServiceClient(HighPerformanceSettings);
    }

    /// <summary>
    /// Creates a client optimized for development and debugging
    /// </summary>
    public static SuggestionServiceClient CreateDevelopmentClient()
    {
        return new SuggestionServiceClient(DevelopmentSettings);
    }

    /// <summary>
    /// Creates a client optimized for reliability over performance
    /// </summary>
    public static SuggestionServiceClient CreateReliableClient()
    {
        return new SuggestionServiceClient(ReliableSettings);
    }

    /// <summary>
    /// Creates a debug client with detailed logging enabled
    /// </summary>
    public static SuggestionServiceClient CreateDebugClient()
    {
        var debugSettings = new ConnectionSettings
        {
            TimeoutMs = 60,
            ConnectionTimeoutMs = 5000,
            MaxRetries = 3,
            RetryDelayMs = 500,
            EnableConnectionPooling = false,
            MaxPoolSize = 2,
            EnableAutoReconnect = false,
            EnableDebugLogging = true
        };
        
        return new SuggestionServiceClient(debugSettings);
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
        return new SuggestionServiceServer(service, "LLMEmpoweredCommandPredictor");
    }

    /// <summary>
    /// Creates a server with custom pipe name and advanced configuration
    /// </summary>
    public static SuggestionServiceServer CreateServer(
        ISuggestionService service, 
        string pipeName, 
        bool enableLogging = true)
    {
        var server = new SuggestionServiceServer(service, pipeName);
        
        // In the future, we could add more server configuration here
        // For now, we'll keep it simple but extensible
        
        return server;
    }
}
