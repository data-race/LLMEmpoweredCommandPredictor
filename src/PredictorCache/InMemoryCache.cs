using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Subsystem.Prediction;
using System.Threading;
using System.Threading.Tasks;

namespace LLMEmpoweredCommandPredictor.PredictorCache;

/// <summary>
/// Configuration options for the in-memory cache.
/// </summary>
public class CacheConfiguration
{
    /// <summary>
    /// Maximum number of cache entries before LRU eviction starts.
    /// </summary>
    public int MaxCapacity { get; init; } = 1000;

    /// <summary>
    /// Default time-to-live for cache entries.
    /// </summary>
    public TimeSpan DefaultTtl { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Interval for automatic cleanup of expired entries.
    /// </summary>
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to enable background cleanup timer.
    /// </summary>
    public bool EnableBackgroundCleanup { get; init; } = true;
}

/// <summary>
/// Internal cache entry containing cached data and metadata.
/// </summary>
internal class CacheEntry
{
    public string Response { get; init; }
    public DateTime ExpirationTime { get; init; }
    public DateTime LastAccessTime { get; set; }
    public DateTime CreatedTime { get; init; }
    
    public CacheEntry(string response)
    {
        Response = response ?? throw new ArgumentNullException(nameof(response));
    }
    
    public bool IsExpired => DateTime.UtcNow > ExpirationTime;
    
    public long EstimatedSizeBytes
    {
        get
        {
            // Rough estimation: response text size + object overhead
            // Multiplies by 2 because .NET strings use UTF-16 encoding (2 bytes per character)
            var textSize = Response.Length * 2; // UTF-16
            return textSize + 200; // Entry overhead
        }
    }
}

/// <summary>
/// High-performance in-memory cache implementation with LRU eviction and TTL expiration.
/// Thread-safe and optimized for PowerShell command prediction scenarios.
/// </summary>
public class InMemoryCache : ICacheService, IDisposable
{
    private readonly CacheConfiguration config;
    private readonly ConcurrentDictionary<string, CacheEntry> cache;
    private readonly LinkedList<string> accessOrder;
    private readonly ReaderWriterLockSlim accessOrderLock;
    private readonly Timer? cleanupTimer;
    
    // Statistics tracking
    private long totalRequests;
    private long cacheHits;
    private long cacheMisses;
    private readonly DateTime startTime;
    private DateTime lastAccessTime;
    
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the InMemoryCache with default configuration.
    /// </summary>
    public InMemoryCache() : this(new CacheConfiguration())
    {
    }

    /// <summary>
    /// Initializes a new instance of the InMemoryCache with custom configuration.
    /// </summary>
    /// <param name="config">Cache configuration options</param>
    public InMemoryCache(CacheConfiguration config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        cache = new ConcurrentDictionary<string, CacheEntry>();
        accessOrder = new LinkedList<string>();
        accessOrderLock = new ReaderWriterLockSlim();
        startTime = DateTime.UtcNow;
        lastAccessTime = startTime;

        // Start background cleanup timer if enabled
        if (this.config.EnableBackgroundCleanup)
        {
            cleanupTimer = new Timer(
                callback: _ => CleanupExpiredEntries(),
                state: null,
                dueTime: this.config.CleanupInterval,
                period: this.config.CleanupInterval);
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return null;

        Interlocked.Increment(ref totalRequests);
        lastAccessTime = DateTime.UtcNow;

        if (cache.TryGetValue(cacheKey, out var entry))
        {
            // Check if entry is expired
            if (entry.IsExpired)
            {
                // Remove expired entry
                await RemoveAsync(cacheKey, cancellationToken);
                Interlocked.Increment(ref cacheMisses);
                return null;
            }

            // Update access time and LRU order
            entry.LastAccessTime = DateTime.UtcNow;
            UpdateAccessOrder(cacheKey);
            
            Interlocked.Increment(ref cacheHits);
            return entry.Response;
        }

        Interlocked.Increment(ref cacheMisses);
        return null;
    }

    /// <inheritdoc />
    public async Task SetAsync(string cacheKey, string response, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cacheKey) || string.IsNullOrEmpty(response))
            return;

        var now = DateTime.UtcNow;
        var entry = new CacheEntry(response)
        {
            ExpirationTime = now.Add(config.DefaultTtl),
            LastAccessTime = now,
            CreatedTime = now
        };

        // Add or update the entry
        cache.AddOrUpdate(cacheKey, entry, (key, oldEntry) => entry);

        // Update LRU order
        UpdateAccessOrder(cacheKey);

        // Enforce capacity limits
        await EnforceCapacityLimits();
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return;

        cache.TryRemove(cacheKey, out _);
        
        // Remove from access order
        accessOrderLock.EnterWriteLock();
        try
        {
            accessOrder.Remove(cacheKey);
        }
        finally
        {
            accessOrderLock.ExitWriteLock();
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cache.Clear();
        
        accessOrderLock.EnterWriteLock();
        try
        {
            accessOrder.Clear();
        }
        finally
        {
            accessOrderLock.ExitWriteLock();
        }

        // Reset statistics (except start time)
        Interlocked.Exchange(ref totalRequests, 0);
        Interlocked.Exchange(ref cacheHits, 0);
        Interlocked.Exchange(ref cacheMisses, 0);

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public CacheStatistics GetStatistics()
    {
        var totalRequestsSnapshot = Interlocked.Read(ref totalRequests);
        var cacheHitsSnapshot = Interlocked.Read(ref cacheHits);
        var cacheMissesSnapshot = Interlocked.Read(ref cacheMisses);
        
        var memoryUsage = cache.Values.Sum(entry => entry.EstimatedSizeBytes);
        
        return new CacheStatistics
        {
            TotalRequests = (int)totalRequestsSnapshot,
            CacheHits = (int)cacheHitsSnapshot,
            CacheMisses = (int)cacheMissesSnapshot,
            TotalEntries = cache.Count,
            MemoryUsageBytes = memoryUsage,
            LastAccess = lastAccessTime,
            Uptime = DateTime.UtcNow - startTime
        };
    }

    /// <summary>
    /// Updates the access order for LRU tracking.
    /// </summary>
    /// <param name="cacheKey">The cache key that was accessed</param>
    private void UpdateAccessOrder(string cacheKey)
    {
        accessOrderLock.EnterWriteLock();
        try
        {
            // Remove existing entry if present
            accessOrder.Remove(cacheKey);
            
            // Add to the end (most recently used)
            accessOrder.AddLast(cacheKey);
        }
        finally
        {
            accessOrderLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Enforces capacity limits by evicting least recently used entries.
    /// </summary>
    private async Task EnforceCapacityLimits()
    {
        if (cache.Count <= config.MaxCapacity)
            return;

        var entriesToRemove = cache.Count - config.MaxCapacity;
        var keysToRemove = new List<string>(entriesToRemove);

        accessOrderLock.EnterReadLock();
        try
        {
            var current = accessOrder.First;
            while (current != null && keysToRemove.Count < entriesToRemove)
            {
                keysToRemove.Add(current.Value);
                current = current.Next;
            }
        }
        finally
        {
            accessOrderLock.ExitReadLock();
        }

        // Remove the LRU entries
        foreach (var key in keysToRemove)
        {
            await RemoveAsync(key);
        }
    }

    /// <summary>
    /// Cleans up expired cache entries.
    /// </summary>
    private void CleanupExpiredEntries()
    {
        if (disposed)
            return;

        var expiredKeys = new List<string>();

        // Find expired entries
        foreach (var kvp in cache)
        {
            if (kvp.Value.IsExpired)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        // Remove expired entries
        foreach (var key in expiredKeys)
        {
            _ = RemoveAsync(key); // Fire and forget
        }
    }

    /// <summary>
    /// Disposes of the cache and cleanup resources.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        
        cleanupTimer?.Dispose();
        accessOrderLock.Dispose();
        cache.Clear();
        
        GC.SuppressFinalize(this);
    }
}
