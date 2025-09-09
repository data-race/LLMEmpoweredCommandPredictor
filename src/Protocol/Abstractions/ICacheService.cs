using System;
using System.Threading;
using System.Threading.Tasks;

namespace LLMEmpoweredCommandPredictor.Protocol.Abstractions;

/// <summary>
/// Abstract interface for cache services in the Protocol layer.
/// This allows Protocol to work with any cache implementation without direct dependencies.
/// </summary>
public interface ICacheService : IDisposable
{
    /// <summary>
    /// Gets cached response for the given cache key.
    /// </summary>
    /// <param name="cacheKey">The cache key to look up</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached response or null if not found/expired</returns>
    Task<string?> GetAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores response in the cache with the given key.
    /// </summary>
    /// <param name="cacheKey">The cache key to store under</param>
    /// <param name="response">The response to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync(string cacheKey, string response, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific cache entry.
    /// </summary>
    /// <param name="cacheKey">The cache key to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cache entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    ICacheStatistics GetStatistics();
}

/// <summary>
/// Abstract interface for cache statistics.
/// </summary>
public interface ICacheStatistics
{
    int TotalRequests { get; }
    int CacheHits { get; }
    int CacheMisses { get; }
    double HitRate { get; }
    int TotalEntries { get; }
    long MemoryUsageBytes { get; }
    DateTime LastAccess { get; }
    TimeSpan Uptime { get; }
}

/// <summary>
/// Abstract interface for cache key generation.
/// </summary>
public interface ICacheKeyGenerator
{
    /// <summary>
    /// Generates a cache key from the suggestion request
    /// </summary>
    /// <param name="request">The suggestion request</param>
    /// <returns>Cache key string</returns>
    string GenerateCacheKey(object request);
}
