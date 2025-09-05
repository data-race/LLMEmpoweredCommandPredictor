using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Management.Automation.Subsystem.Prediction;

namespace LLMEmpoweredCommandPredictor.PredictorCache;

/// <summary>
/// Interface for the suggestion cache service.
/// Provides fast retrieval of previously generated suggestions.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets cached suggestions for the given cache key.
    /// </summary>
    /// <param name="cacheKey">The cache key to look up</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached suggestions or null if not found/expired</returns>
    Task<IReadOnlyList<PredictiveSuggestion>?> GetAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores suggestions in the cache with the given key.
    /// </summary>
    /// <param name="cacheKey">The cache key to store under</param>
    /// <param name="suggestions">The suggestions to cache</param>
    /// <param name="ttl">Time-to-live for the cache entry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync(string cacheKey, IReadOnlyList<PredictiveSuggestion> suggestions, TimeSpan ttl, CancellationToken cancellationToken = default);

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
    CacheStatistics GetStatistics();
}

/// <summary>
/// Cache performance and usage statistics.
/// </summary>
public class CacheStatistics
{
    public int TotalRequests { get; init; }
    public int CacheHits { get; init; }
    public int CacheMisses { get; init; }
    public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests * 100 : 0;
    public int TotalEntries { get; init; }
    public long MemoryUsageBytes { get; init; }
    public DateTime LastAccess { get; init; }
    public TimeSpan Uptime { get; init; }
}
