using System;

namespace LLMEmpoweredCommandPredictor.PredictorCache;

/// <summary>
/// Represents a cache entry containing cached data and metadata.
/// This class encapsulates all information needed for cache management including
/// expiration tracking, access patterns, and memory usage estimation.
/// </summary>
public class CacheEntry
{
    /// <summary>
    /// The cached response data.
    /// </summary>
    public string Response { get; init; }

    /// <summary>
    /// The time when this cache entry expires and should be removed.
    /// </summary>
    public DateTime ExpirationTime { get; init; }

    /// <summary>
    /// The last time this cache entry was accessed.
    /// Used for LRU (Least Recently Used) eviction policy.
    /// </summary>
    public DateTime LastAccessTime { get; set; }

    /// <summary>
    /// The time when this cache entry was created.
    /// </summary>
    public DateTime CreatedTime { get; init; }

    /// <summary>
    /// Initializes a new cache entry with the specified response data.
    /// </summary>
    /// <param name="response">The response data to cache</param>
    /// <exception cref="ArgumentNullException">Thrown when response is null</exception>
    public CacheEntry(string response)
    {
        Response = response ?? throw new ArgumentNullException(nameof(response));
    }

    /// <summary>
    /// Gets a value indicating whether this cache entry has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpirationTime;

    /// <summary>
    /// Gets the estimated size of this cache entry in bytes.
    /// This includes the response text size and estimated object overhead.
    /// </summary>
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

    /// <summary>
    /// Gets the age of this cache entry.
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - CreatedTime;

    /// <summary>
    /// Gets the time since this entry was last accessed.
    /// </summary>
    public TimeSpan TimeSinceLastAccess => DateTime.UtcNow - LastAccessTime;
}
