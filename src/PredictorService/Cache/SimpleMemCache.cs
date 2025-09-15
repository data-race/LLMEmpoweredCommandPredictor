using System;
using System.Collections.Generic;
using System.Linq;

namespace LLMEmpoweredCommandPredictor.PredictorService.Cache;

/// <summary>
/// Simple in-memory cache for storing command suggestions with FIFO eviction policy.
/// </summary>
public class SimpleMemCache
{
    private const int MaxCacheSize = 100;
    private readonly Queue<CacheItem> _cacheQueue = new();
    private readonly Dictionary<string, CacheItem> _cacheDict = new();
    private readonly object _lock = new();

    /// <summary>
    /// Represents a cached command suggestion item.
    /// </summary>
    public class CacheItem
    {
        public string Command { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public DateTime Generated { get; set; }
    }

    /// <summary>
    /// Adds or updates a command suggestion in the cache.
    /// </summary>
    /// <param name="command">The command string</param>
    /// <param name="description">The command description</param>
    /// <param name="confidence">The confidence score (0.0 to 1.0)</param>
    public void AddOrUpdate(string command, string description, float confidence)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        lock (_lock)
        {
            var item = new CacheItem
            {
                Command = command,
                Description = description,
                Confidence = confidence,
                Generated = DateTime.UtcNow
            };

            // If command already exists, remove the old entry
            if (_cacheDict.ContainsKey(command))
            {
                _cacheDict.Remove(command);
                // Note: We don't remove from queue here for simplicity, 
                // the queue will contain some orphaned entries but they'll be cleaned up during eviction
            }

            // Add new item
            _cacheDict[command] = item;
            _cacheQueue.Enqueue(item);

            // Evict oldest items if cache is full (FIFO)
            while (_cacheQueue.Count > MaxCacheSize)
            {
                var oldestItem = _cacheQueue.Dequeue();
                // Only remove from dictionary if it's still the same instance
                // (handles case where command was updated)
                if (_cacheDict.TryGetValue(oldestItem.Command, out var currentItem) && 
                    ReferenceEquals(currentItem, oldestItem))
                {
                    _cacheDict.Remove(oldestItem.Command);
                }
            }
        }
    }

    /// <summary>
    /// Searches for commands that start with the given prefix.
    /// </summary>
    /// <param name="prefix">The prefix to search for</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <returns>List of matching cache items, ordered by confidence (descending)</returns>
    public List<CacheItem> SearchByPrefix(string prefix, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return new List<CacheItem>();

        lock (_lock)
        {
            return _cacheDict.Values
                .Where(item => item.Command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.Confidence)
                .Take(maxResults)
                .ToList();
        }
    }

    /// <summary>
    /// Gets a specific command from the cache.
    /// </summary>
    /// <param name="command">The exact command to retrieve</param>
    /// <returns>The cache item if found, null otherwise</returns>
    public CacheItem? GetCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        lock (_lock)
        {
            return _cacheDict.TryGetValue(command, out var item) ? item : null;
        }
    }

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cacheDict.Clear();
            _cacheQueue.Clear();
        }
    }

    /// <summary>
    /// Gets the current number of items in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _cacheDict.Count;
            }
        }
    }

    /// <summary>
    /// Gets all cached commands, ordered by generation time (newest first).
    /// </summary>
    /// <returns>List of all cache items</returns>
    public List<CacheItem> GetAllCommands()
    {
        lock (_lock)
        {
            return _cacheDict.Values
                .OrderByDescending(item => item.Generated)
                .ToList();
        }
    }
}
