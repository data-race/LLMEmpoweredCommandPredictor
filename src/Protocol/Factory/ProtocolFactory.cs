using LLMEmpoweredCommandPredictor.Protocol.Client;
using LLMEmpoweredCommandPredictor.Protocol.Contracts;
using LLMEmpoweredCommandPredictor.Protocol.Models;
using LLMEmpoweredCommandPredictor.Protocol.Server;
using LLMEmpoweredCommandPredictor.Protocol.Integration;
using LLMEmpoweredCommandPredictor.Protocol.Abstractions;

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
        TimeoutMs = 200,
        ConnectionTimeoutMs = 1000,
        MaxRetries = 3,
        RetryDelayMs = 100,
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
    /// Creates a cached server instance with the specified backend implementation.
    /// Note: Requires cache and key generator implementations to be provided.
    /// </summary>
    /// <param name="backend">Backend service implementation</param>
    /// <param name="cache">Cache service implementation</param>
    /// <param name="keyGenerator">Cache key generator implementation</param>
    /// <param name="pipeName">Named pipe name (optional)</param>
    /// <returns>Configured server instance with caching</returns>
    public static SuggestionServiceServer CreateCachedServer(
        IServiceBackend backend,
        ICacheService cache,
        ICacheKeyGenerator keyGenerator,
        string pipeName = "LLMEmpoweredCommandPredictor.SuggestionService")
    {
        var cachedService = new CachedServiceBridge(backend, cache, keyGenerator);
        return new SuggestionServiceServer(cachedService, pipeName);
    }
}
