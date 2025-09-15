using System;

namespace LLMEmpoweredCommandPredictor.PredictorCache;

/// <summary>
/// Configuration options for the in-memory cache.
/// This class encapsulates all configurable aspects of cache behavior including
/// capacity limits, expiration policies, and background processing options.
/// </summary>
public class CacheConfiguration
{
    /// <summary>
    /// Maximum number of cache entries before LRU eviction starts.
    /// When this limit is reached, the least recently used entries will be removed
    /// to make room for new entries.
    /// </summary>
    public int MaxCapacity { get; init; } = 1000;

    /// <summary>
    /// Default time-to-live for cache entries.
    /// After this duration, cache entries will be considered expired and
    /// will be removed from the cache.
    /// </summary>
    public TimeSpan DefaultTtl { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Interval for automatic cleanup of expired entries.
    /// This determines how frequently the background cleanup process runs
    /// to remove expired entries from the cache.
    /// </summary>
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to enable background cleanup timer.
    /// When enabled, a background timer will automatically remove expired entries
    /// at the interval specified by CleanupInterval.
    /// </summary>
    public bool EnableBackgroundCleanup { get; init; } = true;

    /// <summary>
    /// Whether to enable cache initialization with basic commands.
    /// When enabled, the cache will be pre-populated with common PowerShell
    /// commands during startup. This is useful for production but should be
    /// disabled for testing to ensure clean test state.
    /// </summary>
    public bool EnableInitialization { get; init; } = true;
}
