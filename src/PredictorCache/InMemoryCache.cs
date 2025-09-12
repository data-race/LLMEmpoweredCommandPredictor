using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LLMEmpoweredCommandPredictor.PredictorCache;

/// <summary>
/// History command stored in the prefix cache
/// </summary>
public class HistoryCommand
{
    public string FullCommand { get; set; } = string.Empty;
    public DateTime LastExecuted { get; set; }
    public int ExecutionCount { get; set; }
}

/// <summary>
/// Cache entry for a specific prefix containing historical commands
/// </summary>
public class HistoryCacheEntry
{
    public List<HistoryCommand> Commands { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Simple history-based prefix cache for command prediction.
/// Stores previously executed commands indexed by their prefixes for intelligent completion.
/// </summary>
public class InMemoryCache : ICacheService, IDisposable
{
    private readonly ConcurrentDictionary<string, HistoryCacheEntry> _prefixCache;
    private readonly int _maxCommandsPerPrefix;
    private readonly int _maxPrefixLength;
    
    // Statistics tracking
    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;
    private readonly DateTime _startTime;
    private DateTime _lastAccessTime;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the InMemoryCache with default settings.
    /// </summary>
    public InMemoryCache() : this(maxCommandsPerPrefix: 10, maxPrefixLength: 50)
    {
    }

    /// <summary>
    /// Initializes a new instance of the InMemoryCache with custom settings.
    /// </summary>
    /// <param name="maxCommandsPerPrefix">Maximum number of commands to store per prefix</param>
    /// <param name="maxPrefixLength">Maximum prefix length to index</param>
    public InMemoryCache(int maxCommandsPerPrefix = 10, int maxPrefixLength = 50)
    {
        _prefixCache = new ConcurrentDictionary<string, HistoryCacheEntry>();
        _maxCommandsPerPrefix = maxCommandsPerPrefix;
        _maxPrefixLength = maxPrefixLength;
        _startTime = DateTime.UtcNow;
        _lastAccessTime = _startTime;
    }

    /// <inheritdoc />
    public Task<string?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return Task.FromResult<string?>(null);

        Interlocked.Increment(ref _totalRequests);
        _lastAccessTime = DateTime.UtcNow;

        var userPrefix = NormalizePrefix(cacheKey);

        if (_prefixCache.TryGetValue(userPrefix, out var entry))
        {
            var suggestions = entry.Commands
                .OrderByDescending(c => c.ExecutionCount)
                .ThenByDescending(c => c.LastExecuted)
                .Select(c => c.FullCommand)
                .ToList();

            if (suggestions.Any())
            {
                Interlocked.Increment(ref _cacheHits);
                return Task.FromResult<string?>(string.Join("\n", suggestions));
            }
        }

        Interlocked.Increment(ref _cacheMisses);
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public Task SetAsync(string cacheKey, string response, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return Task.CompletedTask;

        var executedCommand = NormalizePrefix(cacheKey);
        
        // Store this command for all its prefixes AND as an exact match
        for (int i = 1; i <= executedCommand.Length && i <= _maxPrefixLength; i++)
        {
            var prefix = executedCommand.Substring(0, i);

            // Get or create entry for this prefix
            var entry = _prefixCache.GetOrAdd(prefix, _ => new HistoryCacheEntry
            {
                LastUpdated = DateTime.UtcNow
            });

            // Find existing command or create new one
            var existingCommand = entry.Commands.FirstOrDefault(c => c.FullCommand == executedCommand);
            if (existingCommand != null)
            {
                existingCommand.LastExecuted = DateTime.UtcNow;
                existingCommand.ExecutionCount++;
            }
            else
            {
                entry.Commands.Add(new HistoryCommand
                {
                    FullCommand = executedCommand,
                    LastExecuted = DateTime.UtcNow,
                    ExecutionCount = 1
                });
            }

            // Keep most recent/frequent commands
            entry.Commands = entry.Commands
                .OrderByDescending(c => c.ExecutionCount)
                .ThenByDescending(c => c.LastExecuted)
                .Take(_maxCommandsPerPrefix)
                .ToList();

            entry.LastUpdated = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return Task.CompletedTask;

        var normalizedKey = NormalizePrefix(cacheKey);

        // Remove from all prefix entries
        var keysToUpdate = _prefixCache.Keys
            .Where(prefix => normalizedKey.StartsWith(prefix))
            .ToList();

        foreach (var prefixKey in keysToUpdate)
        {
            if (_prefixCache.TryGetValue(prefixKey, out var entry))
            {
                entry.Commands.RemoveAll(c => c.FullCommand == normalizedKey);
                
                // Remove empty entries
                if (!entry.Commands.Any())
                {
                    _prefixCache.TryRemove(prefixKey, out _);
                }
                else
                {
                    entry.LastUpdated = DateTime.UtcNow;
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _prefixCache.Clear();

        // Reset statistics
        Interlocked.Exchange(ref _totalRequests, 0);
        Interlocked.Exchange(ref _cacheHits, 0);
        Interlocked.Exchange(ref _cacheMisses, 0);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public CacheStatistics GetStatistics()
    {
        var totalRequestsSnapshot = Interlocked.Read(ref _totalRequests);
        var cacheHitsSnapshot = Interlocked.Read(ref _cacheHits);
        var cacheMissesSnapshot = Interlocked.Read(ref _cacheMisses);

        var memoryUsage = _prefixCache.Values.Sum(entry => EstimateEntrySize(entry));

        return new CacheStatistics
        {
            TotalRequests = (int)totalRequestsSnapshot,
            CacheHits = (int)cacheHitsSnapshot,
            CacheMisses = (int)cacheMissesSnapshot,
            TotalEntries = _prefixCache.Count,
            MemoryUsageBytes = memoryUsage,
            LastAccess = _lastAccessTime,
            Uptime = DateTime.UtcNow - _startTime
        };
    }

    /// <summary>
    /// Gets all cached commands for debugging purposes
    /// </summary>
    public Dictionary<string, List<string>> GetAllCachedCommands()
    {
        return _prefixCache.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Commands.Select(c => c.FullCommand).ToList()
        );
    }

    /// <summary>
    /// Normalizes a prefix by trimming and converting to lowercase
    /// </summary>
    private string NormalizePrefix(string prefix)
    {
        return prefix?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Estimates the memory size of a cache entry
    /// </summary>
    private long EstimateEntrySize(HistoryCacheEntry entry)
    {
        var commandsSize = entry.Commands.Sum(c => c.FullCommand.Length * 2); // UTF-16
        return commandsSize + 100; // Entry overhead
    }

    /// <summary>
    /// Disposes of the cache and cleanup resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _prefixCache.Clear();
        GC.SuppressFinalize(this);
    }
}
