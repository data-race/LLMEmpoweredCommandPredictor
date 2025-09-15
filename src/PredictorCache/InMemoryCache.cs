using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Subsystem.Prediction;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LLMEmpoweredCommandPredictor.PredictorCache;



/// <summary>
/// High-performance in-memory cache implementation with LRU eviction and TTL expiration.
/// Thread-safe and optimized for PowerShell command prediction scenarios.
/// </summary>
public class InMemoryCache : ICacheService, IDisposable
{
    private readonly CacheConfiguration config;
    private readonly ConcurrentDictionary<string, LinkedList<CacheEntry>> cache;
    private readonly Timer? cleanupTimer;
    private readonly ILogger<InMemoryCache>? logger;

    // Configuration for entries per key
    private const int MaxEntriesPerKey = 10;

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
    /// <param name="logger">Optional logger for debugging</param>
    public InMemoryCache(CacheConfiguration config, ILogger<InMemoryCache>? logger = null)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.logger = logger;
        cache = new ConcurrentDictionary<string, LinkedList<CacheEntry>>();
        startTime = DateTime.UtcNow;
        lastAccessTime = startTime;

        logger?.LogInformation("PredictorCache: InMemoryCache initialized with MaxCapacity={MaxCapacity}, DefaultTtl={DefaultTtl}, CleanupInterval={CleanupInterval}",
            config.MaxCapacity, config.DefaultTtl, config.CleanupInterval);

        // Pre-populate cache with basic commands during initialization
        // NEED TO BE DELETED, ONLY FOR TESTING
        _ = Task.Run(async () => await InitializeCacheWithBasicCommandsAsync());

        // Start background cleanup timer if enabled
        if (this.config.EnableBackgroundCleanup)
        {
            cleanupTimer = new Timer(
                callback: _ => CleanupExpiredEntries(),
                state: null,
                dueTime: this.config.CleanupInterval,
                period: this.config.CleanupInterval);

            logger?.LogDebug("PredictorCache: Background cleanup timer started");
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return null;

        Interlocked.Increment(ref totalRequests);
        lastAccessTime = DateTime.UtcNow;

        logger?.LogDebug("PredictorCache: GetAsync called for key: {CacheKey}", cacheKey);

        if (cache.TryGetValue(cacheKey, out var entryList) && entryList.Count > 0)
        {
            // Get the newest (first) entry
            var entry = entryList.First?.Value;
            if (entry == null)
            {
                Interlocked.Increment(ref cacheMisses);
                return null;
            }

            // Check if entry is expired
            if (entry.IsExpired)
            {
                logger?.LogDebug("PredictorCache: Cache entry expired for key: {CacheKey}", cacheKey);
                // Remove expired entry from the list
                entryList.RemoveFirst();

                // Clean up any other expired entries from the front
                await CleanupExpiredEntriesFromListAsync(entryList);

                // If list is empty, remove the key entirely
                if (entryList.Count == 0)
                {
                    cache.TryRemove(cacheKey, out _);
                }

                Interlocked.Increment(ref cacheMisses);
                return null;
            }

            // Update access time
            entry.LastAccessTime = DateTime.UtcNow;

            Interlocked.Increment(ref cacheHits);
            logger?.LogDebug("PredictorCache: Cache HIT for key: {CacheKey}", cacheKey);
            return entry.Response;
        }

        Interlocked.Increment(ref cacheMisses);
        logger?.LogDebug("PredictorCache: Cache MISS for key: {CacheKey}", cacheKey);
        return null;
    }

    /// <summary>
    /// Attempts to find cached suggestions using prefix matching.
    /// Searches through multiple prefix keys to find the best match.
    /// </summary>
    public async Task<string?> GetByPrefixAsync(List<string> prefixKeys, CancellationToken cancellationToken = default)
    {
        if (prefixKeys == null || !prefixKeys.Any())
            return null;

        Interlocked.Increment(ref totalRequests);
        lastAccessTime = DateTime.UtcNow;

        // Try each prefix key in order (longest first)
        foreach (var key in prefixKeys)
        {
            if (cache.TryGetValue(key, out var entryList) && entryList.Count > 0)
            {
                var entry = entryList.First?.Value;
                if (entry == null)
                    continue;

                // Check if entry is expired
                if (entry.IsExpired)
                {
                    // Remove expired entry and continue
                    await RemoveAsync(key, cancellationToken);
                    continue;
                }

                // Found a valid cache entry
                entry.LastAccessTime = DateTime.UtcNow;

                Interlocked.Increment(ref cacheHits);
                return entry.Response;
            }
        }

        Interlocked.Increment(ref cacheMisses);
        return null;
    }

    /// <summary>
    /// Stores response with multiple prefix keys for better matching
    /// </summary>
    public async Task SetWithPrefixKeysAsync(List<string> keys, string response, CancellationToken cancellationToken = default)
    {
        if (keys == null || !keys.Any() || string.IsNullOrEmpty(response))
            return;

        // Store the response under each prefix key using SetAsync
        foreach (var key in keys)
        {
            await SetAsync(key, response, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(string cacheKey, string response, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cacheKey) || string.IsNullOrEmpty(response))
            return;

        logger?.LogDebug("PredictorCache: SetAsync called for key: {CacheKey}, response length: {ResponseLength}",
            cacheKey, response.Length);

        var now = DateTime.UtcNow;
        var entry = new CacheEntry(response)
        {
            ExpirationTime = now.Add(config.DefaultTtl),
            LastAccessTime = now,
            CreatedTime = now
        };

        // Add entry to LinkedList (newest entries go to the front)
        cache.AddOrUpdate(cacheKey,
            // Create new LinkedList with this entry if key doesn't exist
            new LinkedList<CacheEntry>(new[] { entry }),
            // If key exists, add this entry to the front of existing list
            (key, existingList) =>
            {
                existingList.AddFirst(entry);

                // Enforce per-key entry limit
                while (existingList.Count > MaxEntriesPerKey)
                {
                    existingList.RemoveLast(); // Remove oldest entries
                }

                return existingList;
            });

        logger?.LogDebug("PredictorCache: Cache entry stored for key: {CacheKey}, total keys: {TotalKeys}",
            cacheKey, cache.Count);

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return;

        cache.TryRemove(cacheKey, out _);

        logger?.LogDebug("PredictorCache: Removed cache key: {CacheKey}", cacheKey);

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cache.Clear();

        // Reset statistics (except start time)
        Interlocked.Exchange(ref totalRequests, 0);
        Interlocked.Exchange(ref cacheHits, 0);
        Interlocked.Exchange(ref cacheMisses, 0);

        logger?.LogDebug("PredictorCache: Cache cleared");

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public CacheStatistics GetStatistics()
    {
        var totalRequestsSnapshot = Interlocked.Read(ref totalRequests);
        var cacheHitsSnapshot = Interlocked.Read(ref cacheHits);
        var cacheMissesSnapshot = Interlocked.Read(ref cacheMisses);

        // Calculate memory usage by summing all entries in all LinkedLists
        var memoryUsage = 0L;
        var totalEntries = 0;

        foreach (var entryList in cache.Values)
        {
            totalEntries += entryList.Count;
            foreach (var entry in entryList)
            {
                memoryUsage += entry.EstimatedSizeBytes;
            }
        }

        return new CacheStatistics
        {
            TotalRequests = (int)totalRequestsSnapshot,
            CacheHits = (int)cacheHitsSnapshot,
            CacheMisses = (int)cacheMissesSnapshot,
            TotalEntries = totalEntries,
            MemoryUsageBytes = memoryUsage,
            LastAccess = lastAccessTime,
            Uptime = DateTime.UtcNow - startTime
        };
    }

    /// <summary>
    /// Cleans up expired cache entries from all LinkedLists in the cache.
    /// This is called by the background timer.
    /// </summary>
    private void CleanupExpiredEntries()
    {
        if (disposed)
            return;

        logger?.LogInformation("PredictorCache: Background cleanup started at {Time}", DateTime.UtcNow);

        var keysToRemove = new List<string>();
        var cleanedCount = 0;

        try
        {
            // Iterate through all cache keys
            foreach (var kvp in cache)
            {
                var key = kvp.Key;
                var entryList = kvp.Value;

                if (entryList == null || entryList.Count == 0)
                {
                    keysToRemove.Add(key);
                    continue;
                }

                // Clean expired entries from this list (from back to front since oldest are at back)
                var currentNode = entryList.Last;
                while (currentNode != null)
                {
                    var prevNode = currentNode.Previous;

                    if (currentNode.Value.IsExpired)
                    {
                        entryList.Remove(currentNode);
                        cleanedCount++;
                    }
                    else
                    {
                        // Since we're going from newest to oldest, if this entry is not expired,
                        // all entries before it are also not expired (they're newer)
                        break;
                    }

                    currentNode = prevNode;
                }

                // If list is empty after cleanup, mark key for removal
                if (entryList.Count == 0)
                {
                    keysToRemove.Add(key);
                }
            }

            // Remove empty keys
            foreach (var key in keysToRemove)
            {
                cache.TryRemove(key, out _);
            }

            if (cleanedCount > 0 || keysToRemove.Count > 0)
            {
                logger?.LogDebug("PredictorCache: Background cleanup removed {CleanedEntries} expired entries and {EmptyKeys} empty keys",
                    cleanedCount, keysToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "PredictorCache: Error during background cleanup");
        }
    }

    /// <summary>
    /// Helper method to clean up expired entries from a specific LinkedList.
    /// Used during on-access cleanup.
    /// </summary>
    private async Task CleanupExpiredEntriesFromListAsync(LinkedList<CacheEntry> entryList)
    {
        if (entryList == null || entryList.Count == 0)
            return;

        // Clean from the front (newest entries) until we find a non-expired entry
        while (entryList.Count > 0 && entryList.First?.Value?.IsExpired == true)
        {
            entryList.RemoveFirst();
        }

        await Task.CompletedTask;
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
        cache.Clear();

        GC.SuppressFinalize(this);
    }
    
    /// ----------------------------- TO BE DELETED WHEN SETASYNC IS PROPERLY CALLED -----------------------------
    /// <summary>
    /// Pre-populates the cache with basic PowerShell commands using prefix-based keys.
    /// This ensures SetAsync is called and cache has useful suggestions from startup.
    /// TEMPORARY METHOD - TO BE DELETED WHEN PROPER COMMAND DETECTION IS IMPLEMENTED
    /// </summary>
    private async Task InitializeCacheWithBasicCommandsAsync()
    {
        try
        {
            logger?.LogInformation("PredictorCache: Starting cache initialization with basic commands");

            // Define basic command patterns and their suggestions (as simple JSON without PredictiveSuggestion objects)
            var basicCommands = new Dictionary<string, string>
            {
                // Git commands
                {
                    "git",
                    """{"Suggestions":["git status","git add .","git commit -m \"message\"","git push","git pull"],"Source":"initialization","IsFromCache":false,"GenerationTimeMs":1.0}"""
                },
                {
                    "git_s",
                    """{"Suggestions":["git status","git stash","git show","git switch"],"Source":"initialization","IsFromCache":false,"GenerationTimeMs":1.0}"""
                },
                {
                    "git_st",
                    """{"Suggestions":["git status","git stash","git stash pop"],"Source":"initialization","IsFromCache":false,"GenerationTimeMs":1.0}"""
                },
                // PowerShell Get- commands
                {
                    "get-",
                    """{"Suggestions":["Get-Process","Get-Service","Get-ChildItem","Get-Content","Get-Location"],"Source":"initialization","IsFromCache":false,"GenerationTimeMs":1.0}"""
                },
                {
                    "get-p",
                    """{"Suggestions":["Get-Process","Get-Process | Sort-Object CPU -Descending","Get-Process | Where-Object {$_.ProcessName -like '*pattern*'}"],"Source":"initialization","IsFromCache":false,"GenerationTimeMs":1.0}"""
                },
                // Docker commands
                {
                    "docker",
                    """{"Suggestions":["docker ps","docker images","docker run","docker build","docker stop"],"Source":"initialization","IsFromCache":false,"GenerationTimeMs":1.0}"""
                },
                {
                    "docker_p",
                    """{"Suggestions":["docker ps","docker ps -a","docker pull"],"Source":"initialization","IsFromCache":false,"GenerationTimeMs":1.0}"""
                },
                // Common PowerShell patterns
                {
                    "ls",
                    """{"Suggestions":["ls -la","ls | Sort-Object Name","ls | Where-Object {$_.Length -gt 1MB}"],"Source":"initialization","IsFromCache":false,"GenerationTimeMs":1.0}"""
                }
            };

            // Cache each command pattern using SetAsync
            foreach (var (cacheKey, response) in basicCommands)
            {
                try
                {
                    // Use SetAsync to store the data (this will call the method we want to test)
                    await SetAsync(cacheKey, response);

                    logger?.LogDebug("PredictorCache: Initialized cache entry for key: {CacheKey}", cacheKey);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning("PredictorCache: Failed to cache suggestions for '{CacheKey}': {Error}",
                        cacheKey, ex.Message);
                }
            }

            // Log cache statistics after initialization
            var stats = GetStatistics();
            logger?.LogInformation("PredictorCache: Cache initialization complete. Total entries: {TotalEntries}, Memory usage: {MemoryUsageMB:F2} MB",
                stats.TotalEntries, stats.MemoryUsageBytes / (1024.0 * 1024.0));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "PredictorCache: Error during cache initialization");
        }
    }
}
