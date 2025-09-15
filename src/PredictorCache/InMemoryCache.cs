using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LLMEmpoweredCommandPredictor.PredictorCache;

/// <summary>
/// High-performance in-memory cache implementation with prefix-based storage and LRU eviction.
/// Thread-safe and optimized for PowerShell command prediction scenarios.
/// </summary>
public class InMemoryCache : ICacheService, IDisposable
{
    private readonly ConcurrentDictionary<string, LinkedList<CacheEntry>> cache;
    private readonly ConcurrentDictionary<string, DateTime> keyLastAccess;
    private readonly Timer? cleanupTimer;
    private readonly object lockObject = new object();
    private readonly ILogger<InMemoryCache> logger;

    // Configuration constants
    private const int MAX_PREFIX_LENGTH = 50;
    private const int MAX_TOTAL_KEYS = 1000;
    private const int MAX_ENTRIES_PER_KEY = 2; //10;
    private const int MAX_SUGGESTIONS_RETURNED = 5;
    private static readonly TimeSpan DEFAULT_TTL = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CLEANUP_INTERVAL = TimeSpan.FromMinutes(5);

    // Statistics tracking
    private long totalRequests;
    private long cacheHits;
    private long cacheMisses;
    private readonly DateTime startTime;
    private DateTime lastAccessTime;

    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the InMemoryCache.
    /// </summary>
    public InMemoryCache()
    {
        logger = ConsoleLoggerFactory.CreateCacheDebugLogger<InMemoryCache>();
        cache = new ConcurrentDictionary<string, LinkedList<CacheEntry>>();
        keyLastAccess = new ConcurrentDictionary<string, DateTime>();
        startTime = DateTime.UtcNow;
        lastAccessTime = startTime;

        logger.LogInformation("InMemoryCache initialized at {StartTime}", startTime);
        logger.LogDebug("Cache configuration: MAX_TOTAL_KEYS={MaxKeys}, MAX_ENTRIES_PER_KEY={MaxEntriesPerKey}, DEFAULT_TTL={DefaultTtl}, CLEANUP_INTERVAL={CleanupInterval}", 
            MAX_TOTAL_KEYS, MAX_ENTRIES_PER_KEY, DEFAULT_TTL, CLEANUP_INTERVAL);

        // Pre-populate cache with basic commands during initialization
        _ = Task.Run(async () => await InitializeCacheWithBasicCommandsAsync());

        // Start background cleanup timer
        cleanupTimer = new Timer(
            callback: _ => CleanupExpiredEntries(),
            state: null,
            dueTime: CLEANUP_INTERVAL,
            period: CLEANUP_INTERVAL);
        
        logger.LogDebug("Background cleanup timer started with interval {CleanupInterval}", CLEANUP_INTERVAL);
    }

    /// <summary>
    /// Initializes a new instance of the InMemoryCache with custom configuration.
    /// This constructor exists for backward compatibility with tests.
    /// </summary>
    /// <param name="config">Cache configuration options (ignored in new implementation)</param>
    public InMemoryCache(CacheConfiguration config) : this()
    {
        // Just ignore the config parameter and use our built-in constants
    }

    /// <summary>
    /// Initializes a new instance of the InMemoryCache with custom configuration and logger.
    /// Both parameters are ignored and the default constructor is called.
    /// </summary>
    /// <param name="config">Cache configuration options (ignored in new implementation)</param>
    /// <param name="logger">Logger instance (ignored, uses built-in cache logger)</param>
    public InMemoryCache(CacheConfiguration config, ILogger<InMemoryCache> logger) : this()
    {
        // Just ignore both parameters and use the default constructor
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("GetAsync called with cacheKey: '{CacheKey}'", cacheKey);

        if (string.IsNullOrEmpty(cacheKey))
        {
            logger.LogDebug("GetAsync returning null - empty or null cache key");
            return null;
        }

        Interlocked.Increment(ref totalRequests);
        lastAccessTime = DateTime.UtcNow;

        // Normalize the cache key
        var normalizedKey = NormalizeKey(cacheKey);
        logger.LogDebug("Normalized cache key: '{NormalizedKey}' (from '{OriginalKey}')", normalizedKey, cacheKey);

        if (cache.TryGetValue(normalizedKey, out var entryList) && entryList.Count > 0)
        {
            logger.LogDebug("Cache key found with {EntryCount} entries", entryList.Count);

            // Update access time for this specific key
            keyLastAccess.TryAdd(normalizedKey, DateTime.UtcNow);
            keyLastAccess[normalizedKey] = DateTime.UtcNow;

            // Clean up expired entries first
            var entriesBeforeCleanup = entryList.Count;
            await CleanupExpiredEntriesFromListAsync(entryList);
            var entriesAfterCleanup = entryList.Count;
            
            if (entriesBeforeCleanup != entriesAfterCleanup)
            {
                logger.LogDebug("Cleanup removed {RemovedEntries} expired entries ({BeforeCount} -> {AfterCount})", 
                    entriesBeforeCleanup - entriesAfterCleanup, entriesBeforeCleanup, entriesAfterCleanup);
            }

            if (entryList.Count == 0)
            {
                // All entries were expired, remove the key
                cache.TryRemove(normalizedKey, out _);
                keyLastAccess.TryRemove(normalizedKey, out _);
                Interlocked.Increment(ref cacheMisses);
                logger.LogDebug("Cache MISS - all entries expired for key '{NormalizedKey}'", normalizedKey);
                return null;
            }

            // Get the last (most recent) entries - up to MAX_SUGGESTIONS_RETURNED
            var recentEntries = entryList.TakeLast(MAX_SUGGESTIONS_RETURNED).ToList();
            logger.LogDebug("Retrieved {RecentEntryCount} recent entries (max {MaxSuggestions})", 
                recentEntries.Count, MAX_SUGGESTIONS_RETURNED);
            
            if (recentEntries.Any())
            {
                // Update access times for returned entries
                foreach (var entry in recentEntries)
                {
                    entry.LastAccessTime = DateTime.UtcNow;
                }

                // Return the response from the most recent entry
                var mostRecentEntry = recentEntries.Last();
                Interlocked.Increment(ref cacheHits);
                
                var responseLength = mostRecentEntry.Response?.Length ?? 0;
                logger.LogDebug("Cache HIT for key '{NormalizedKey}' - returning response ({ResponseLength} chars, created: {CreatedTime})", 
                    normalizedKey, responseLength, mostRecentEntry.CreatedTime);
                
                return mostRecentEntry.Response;
            }
        }
        else
        {
            logger.LogDebug("Cache key '{NormalizedKey}' not found in cache", normalizedKey);
        }

        Interlocked.Increment(ref cacheMisses);
        logger.LogDebug("Cache MISS for key '{NormalizedKey}' - no valid entries found", normalizedKey);
        
        // Log current cache statistics
        var stats = GetStatistics();
        logger.LogDebug("Current cache stats: {TotalRequests} requests, {CacheHits} hits, {CacheMisses} misses, {TotalEntries} entries, {CacheCount} keys", 
            stats.TotalRequests, stats.CacheHits, stats.CacheMisses, stats.TotalEntries, cache.Count);
            
        return null;
    }

    /// <inheritdoc />
    public async Task SetAsync(string command, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("SetAsync called with command: '{Command}'", command);

        if (string.IsNullOrEmpty(command))
        {
            logger.LogDebug("SetAsync returning early - empty command");
            return;
        }

        var normalizedCommand = NormalizeKey(command);
        var now = DateTime.UtcNow;
        logger.LogDebug("Normalized command: '{NormalizedCommand}' (from '{OriginalKey}')", normalizedCommand, command);

        // Generate all prefix keys for this command
        var prefixKeys = GenerateAllPrefixKeys(normalizedCommand);
        logger.LogDebug("Generated {PrefixKeyCount} prefix keys for command (max length: {MaxPrefixLength}): [{PrefixKeys}]", 
            prefixKeys.Count, MAX_PREFIX_LENGTH, string.Join(", ", prefixKeys.Take(5).Select(k => $"'{k}'")));

        // Check if we need to perform LRU cleanup before adding new entries
        var cacheCountBeforeCleanup = cache.Count;
        if (cache.Count + prefixKeys.Count > MAX_TOTAL_KEYS)
        {
            logger.LogDebug("Cache size limit approached ({CurrentCount} + {NewKeys} > {MaxKeys}), performing LRU cleanup", 
                cache.Count, prefixKeys.Count, MAX_TOTAL_KEYS);
            await PerformLRUCleanupAsync();
            logger.LogDebug("LRU cleanup completed ({BeforeCount} -> {AfterCount} keys)", 
                cacheCountBeforeCleanup, cache.Count);
        }

        var entriesAdded = 0;
        var entriesUpdated = 0;

        // Store a separate CacheEntry for each prefix key
        foreach (var prefixKey in prefixKeys)
        {
            // Create a new CacheEntry for this prefix
            var entry = new CacheEntry(command)
            {
                ExpirationTime = now.Add(DEFAULT_TTL),
                LastAccessTime = now,
                CreatedTime = now
            };

            var wasNewKey = false;
            var entriesRemovedDueToLimit = 0;

            // Add entry to the LinkedList for this prefix (newest entries go to the back)
            cache.AddOrUpdate(prefixKey,
                // Create new LinkedList with this entry if key doesn't exist
                addValueFactory: key =>
                {
                    wasNewKey = true;
                    entriesAdded++;
                    return new LinkedList<CacheEntry>(new[] { entry });
                },
                // If key exists, add this entry to the back of existing list
                updateValueFactory: (key, existingList) =>
                {
                    lock (existingList)
                    {
                        var entriesBeforeAdd = existingList.Count;
                        existingList.AddLast(entry);
                        entriesUpdated++;

                        // Enforce per-key entry limit (remove oldest entries from front)
                        while (existingList.Count > MAX_ENTRIES_PER_KEY)
                        {
                            existingList.RemoveFirst();
                            entriesRemovedDueToLimit++;
                        }

                        if (entriesRemovedDueToLimit > 0)
                        {
                            logger.LogDebug("Removed {RemovedCount} old entries for prefix '{PrefixKey}' due to limit ({BeforeCount} -> {AfterCount})", 
                                entriesRemovedDueToLimit, prefixKey, entriesBeforeAdd + 1, existingList.Count);
                        }
                    }
                    return existingList;
                });

            // Update access time for this prefix key
            keyLastAccess.TryAdd(prefixKey, now);
            keyLastAccess[prefixKey] = now;

            if (wasNewKey)
            {
                logger.LogDebug("Created new cache entry for prefix '{PrefixKey}' (expires: {ExpirationTime})", 
                    prefixKey, entry.ExpirationTime);
            }
        }

        logger.LogDebug("SetAsync completed - added {EntriesAdded} new keys, updated {EntriesUpdated} existing keys for command '{NormalizedCommand}'", 
            entriesAdded, entriesUpdated, normalizedCommand);

        // Log cache statistics after adding entries
        var stats = GetStatistics();
        logger.LogDebug("Cache stats after SetAsync: {TotalEntries} entries, {CacheCount} keys, {MemoryUsage} bytes", 
            stats.TotalEntries, cache.Count, stats.MemoryUsageBytes);

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("RemoveAsync called with cacheKey: '{CacheKey}'", cacheKey);

        if (string.IsNullOrEmpty(cacheKey))
        {
            logger.LogDebug("RemoveAsync returning early - empty or null cache key");
            return;
        }

        var normalizedKey = NormalizeKey(cacheKey);
        logger.LogDebug("Normalized key for removal: '{NormalizedKey}' (from '{OriginalKey}')", normalizedKey, cacheKey);
        
        // Remove the specific key
        var wasRemoved = cache.TryRemove(normalizedKey, out var removedEntries);
        var accessTimeRemoved = keyLastAccess.TryRemove(normalizedKey, out _);
        
        if (wasRemoved)
        {
            var entryCount = removedEntries?.Count ?? 0;
            logger.LogDebug("Successfully removed cache key '{NormalizedKey}' with {EntryCount} entries", normalizedKey, entryCount);
        }
        else
        {
            logger.LogDebug("Cache key '{NormalizedKey}' was not found for removal", normalizedKey);
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("ClearAsync called - clearing all cache entries");

        var cacheKeysBeforeClear = cache.Count;
        var accessKeysBeforeClear = keyLastAccess.Count;

        cache.Clear();
        keyLastAccess.Clear();

        // Reset statistics (except start time)
        var oldTotalRequests = Interlocked.Exchange(ref totalRequests, 0);
        var oldCacheHits = Interlocked.Exchange(ref cacheHits, 0);
        var oldCacheMisses = Interlocked.Exchange(ref cacheMisses, 0);

        logger.LogInformation("Cache cleared - removed {CacheKeys} cache keys, {AccessKeys} access keys. Reset stats: {TotalRequests} requests, {CacheHits} hits, {CacheMisses} misses", 
            cacheKeysBeforeClear, accessKeysBeforeClear, oldTotalRequests, oldCacheHits, oldCacheMisses);

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
            lock (entryList)
            {
                totalEntries += entryList.Count;
                foreach (var entry in entryList)
                {
                    memoryUsage += entry.EstimatedSizeBytes;
                }
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
    /// Generates all prefix keys for a given command (1 character to min(command.Length, MAX_PREFIX_LENGTH))
    /// </summary>
    private List<string> GenerateAllPrefixKeys(string command)
    {
        var keys = new List<string>();
        
        if (string.IsNullOrEmpty(command))
            return keys;

        var maxLength = Math.Min(command.Length, MAX_PREFIX_LENGTH);
        
        for (int i = 1; i <= maxLength; i++)
        {
            keys.Add(command.Substring(0, i));
        }

        return keys;
    }

    /// <summary>
    /// Normalizes a cache key for consistent storage and lookup
    /// </summary>
    private string NormalizeKey(string key)
    {
        return key?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Performs LRU cleanup by removing the least recently accessed keys
    /// </summary>
    private async Task PerformLRUCleanupAsync()
    {
        try
        {
            var keysBeforeCleanup = cache.Count;
            logger.LogDebug("Starting LRU cleanup - current cache has {KeyCount} keys", keysBeforeCleanup);

            // Find the oldest 20% of keys to remove
            var keysToRemove = keyLastAccess
                .OrderBy(kvp => kvp.Value)
                .Take(cache.Count / 5) // Remove 20%
                .Select(kvp => kvp.Key)
                .ToList();

            logger.LogDebug("LRU cleanup will remove {KeysToRemoveCount} oldest keys (20% of {TotalKeys})", 
                keysToRemove.Count, keysBeforeCleanup);

            var actuallyRemoved = 0;
            foreach (var key in keysToRemove)
            {
                if (cache.TryRemove(key, out var removedEntries))
                {
                    actuallyRemoved++;
                    var entryCount = removedEntries?.Count ?? 0;
                    logger.LogDebug("LRU removed key '{Key}' with {EntryCount} entries (last access: {LastAccess})", 
                        key, entryCount, keyLastAccess.TryGetValue(key, out var lastAccess) ? lastAccess : DateTime.MinValue);
                }
                keyLastAccess.TryRemove(key, out _);
            }

            logger.LogDebug("LRU cleanup completed - removed {ActuallyRemoved}/{PlannedToRemove} keys ({BeforeCount} -> {AfterCount})", 
                actuallyRemoved, keysToRemove.Count, keysBeforeCleanup, cache.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error during LRU cleanup");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up expired cache entries from all LinkedLists in the cache.
    /// This is called by the background timer.
    /// </summary>
    private void CleanupExpiredEntries()
    {
        if (disposed)
            return;

        var startTime = DateTime.UtcNow;
        var keysToRemove = new List<string>();
        var totalExpiredEntries = 0;
        var keysProcessed = 0;

        try
        {
            logger.LogDebug("Starting background cleanup of expired entries - cache has {KeyCount} keys", cache.Count);

            foreach (var kvp in cache)
            {
                var key = kvp.Key;
                var entryList = kvp.Value;
                keysProcessed++;

                if (entryList == null)
                {
                    keysToRemove.Add(key);
                    logger.LogDebug("Found null entry list for key '{Key}' - marking for removal", key);
                    continue;
                }

                lock (entryList)
                {
                    var entriesBeforeCleanup = entryList.Count;
                    
                    // Remove expired entries from the front (oldest first)
                    while (entryList.Count > 0 && entryList.First?.Value?.IsExpired == true)
                    {
                        entryList.RemoveFirst();
                        totalExpiredEntries++;
                    }

                    var entriesAfterCleanup = entryList.Count;
                    var expiredInThisKey = entriesBeforeCleanup - entriesAfterCleanup;

                    if (expiredInThisKey > 0)
                    {
                        logger.LogDebug("Removed {ExpiredCount} expired entries from key '{Key}' ({BeforeCount} -> {AfterCount})", 
                            expiredInThisKey, key, entriesBeforeCleanup, entriesAfterCleanup);
                    }

                    // If list is empty after cleanup, mark key for removal
                    if (entryList.Count == 0)
                    {
                        keysToRemove.Add(key);
                        logger.LogDebug("Key '{Key}' is empty after cleanup - marking for removal", key);
                    }
                }
            }

            // Remove empty keys
            var keysActuallyRemoved = 0;
            foreach (var key in keysToRemove)
            {
                if (cache.TryRemove(key, out _))
                {
                    keysActuallyRemoved++;
                }
                keyLastAccess.TryRemove(key, out _);
            }

            var duration = DateTime.UtcNow - startTime;
            logger.LogDebug("Background cleanup completed in {Duration}ms - processed {KeysProcessed} keys, removed {ExpiredEntries} expired entries, removed {EmptyKeys} empty keys", 
                duration.TotalMilliseconds, keysProcessed, totalExpiredEntries, keysActuallyRemoved);

            if (totalExpiredEntries > 0 || keysActuallyRemoved > 0)
            {
                logger.LogInformation("Cache cleanup removed {ExpiredEntries} expired entries and {EmptyKeys} empty keys in {Duration}ms", 
                    totalExpiredEntries, keysActuallyRemoved, duration.TotalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error during background cleanup of expired entries");
        }
    }

    /// <summary>
    /// Helper method to clean up expired entries from a specific LinkedList.
    /// </summary>
    private async Task CleanupExpiredEntriesFromListAsync(LinkedList<CacheEntry> entryList)
    {
        if (entryList == null)
            return;

        lock (entryList)
        {
            // Remove expired entries from the front (oldest entries)
            while (entryList.Count > 0 && entryList.First?.Value?.IsExpired == true)
            {
                entryList.RemoveFirst();
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Pre-populates the cache with basic PowerShell commands.
    /// </summary>
    private async Task InitializeCacheWithBasicCommandsAsync()
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            logger.LogDebug("Starting cache initialization with basic commands");

            // Define basic commands as a list of command strings
            var basicCommands = new List<string>
            {
                "git status",
                "git add",
                "git commit",
                "git push",
                "git pull",
                "git branch",
                "git checkout",
                "get-process",
                "get-service",
                "get-childitem",
                "docker ps",
                "docker run",
                "ls",
                "cd" 
            };

            logger.LogDebug("Initializing cache with {CommandCount} basic commands: [{Commands}]", 
                basicCommands.Count, string.Join(", ", basicCommands.Take(5).Select(c => $"'{c}'")));

            var successfullyAdded = 0;
            var errors = 0;

            // Cache each command using SetAsync
            foreach (var command in basicCommands)
            {
                try
                {
                    await SetAsync(command);
                    successfullyAdded++;
                    logger.LogDebug("Successfully initialized cache entry for command '{Command}'", command);
                }
                catch (Exception ex)
                {
                    errors++;
                    logger.LogWarning(ex, "Failed to initialize cache entry for command '{Command}'", command);
                }
            }

            var duration = DateTime.UtcNow - startTime;
            logger.LogInformation("Cache initialization completed in {Duration}ms - successfully added {SuccessCount}/{TotalCount} commands ({ErrorCount} errors)", 
                duration.TotalMilliseconds, successfullyAdded, basicCommands.Count, errors);

            // Log final cache statistics after initialization
            var stats = GetStatistics();
            logger.LogDebug("Cache stats after initialization: {TotalEntries} entries, {CacheCount} keys, {MemoryUsage} bytes", 
                stats.TotalEntries, cache.Count, stats.MemoryUsageBytes);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            logger.LogError(ex, "Cache initialization failed after {Duration}ms", duration.TotalMilliseconds);
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
        cache.Clear();
        keyLastAccess.Clear();

        GC.SuppressFinalize(this);
    }
}
