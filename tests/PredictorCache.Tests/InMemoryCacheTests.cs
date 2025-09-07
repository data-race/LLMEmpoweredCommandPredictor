using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LLMEmpoweredCommandPredictor.PredictorCache;
using Xunit;

namespace LLMEmpoweredCommandPredictor.PredictorCache.Tests;

public class InMemoryCacheTests : IDisposable
{
    private readonly InMemoryCache cache;
    private readonly CacheConfiguration testConfig;

    public InMemoryCacheTests()
    {
        testConfig = new CacheConfiguration
        {
            MaxCapacity = 5,
            DefaultTtl = TimeSpan.FromSeconds(30),
            CleanupInterval = TimeSpan.FromMilliseconds(100),
            EnableBackgroundCleanup = true
        };
        cache = new InMemoryCache(testConfig);
    }

    [Fact]
    public async Task GetAsync_WithNullKey_ReturnsNull()
    {
        // Act
        var result = await cache.GetAsync(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WithEmptyKey_ReturnsNull()
    {
        // Act
        var result = await cache.GetAsync("");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WithNonExistentKey_ReturnsNull()
    {
        // Act
        var result = await cache.GetAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_AndGetAsync_ReturnsStoredResponse()
    {
        // Arrange
        var response = "test response with multiple suggestions";
        var key = "test-key";

        // Act
        await cache.SetAsync(key, response);
        var result = await cache.GetAsync(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(response, result);
    }

    [Fact]
    public async Task SetAsync_WithNullResponse_DoesNotStore()
    {
        // Act
        await cache.SetAsync("key", null);
        var result = await cache.GetAsync("key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_WithEmptyResponse_DoesNotStore()
    {
        // Act
        await cache.SetAsync("key", "");
        var result = await cache.GetAsync("key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WithExpiredEntry_ReturnsNull()
    {
        // Arrange - Create cache with very short TTL
        var shortTtlConfig = new CacheConfiguration
        {
            DefaultTtl = TimeSpan.FromMilliseconds(50),
            EnableBackgroundCleanup = true,
            CleanupInterval = TimeSpan.FromMilliseconds(25)
        };
        using var shortTtlCache = new InMemoryCache(shortTtlConfig);
        
        var response = "test response";
        var key = "expired-key";

        // Act
        await shortTtlCache.SetAsync(key, response);
        await Task.Delay(100); // Wait for expiration
        var result = await shortTtlCache.GetAsync(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_RemovesEntry()
    {
        // Arrange
        var response = "test response";
        var key = "remove-key";

        await cache.SetAsync(key, response);
        
        // Verify it exists
        var beforeRemove = await cache.GetAsync(key);
        Assert.NotNull(beforeRemove);

        // Act
        await cache.RemoveAsync(key);
        var afterRemove = await cache.GetAsync(key);

        // Assert
        Assert.Null(afterRemove);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllEntries()
    {
        // Arrange
        await cache.SetAsync("key1", "response1");
        await cache.SetAsync("key2", "response2");

        // Verify entries exist
        Assert.NotNull(await cache.GetAsync("key1"));
        Assert.NotNull(await cache.GetAsync("key2"));

        // Act
        await cache.ClearAsync();

        // Assert
        Assert.Null(await cache.GetAsync("key1"));
        Assert.Null(await cache.GetAsync("key2"));
    }

    [Fact]
    public async Task LRU_Eviction_RemovesOldestEntries()
    {
        // Arrange - Fill cache to capacity
        for (int i = 0; i < testConfig.MaxCapacity; i++)
        {
            await cache.SetAsync($"key{i}", $"response{i}");
        }

        // Act - Add one more entry to trigger eviction
        await cache.SetAsync("new-key", "new-response");

        // Assert - First entry should be evicted
        Assert.Null(await cache.GetAsync("key0"));
        Assert.NotNull(await cache.GetAsync("new-key"));
        
        // Other entries should still exist
        for (int i = 1; i < testConfig.MaxCapacity; i++)
        {
            Assert.NotNull(await cache.GetAsync($"key{i}"));
        }
    }

    [Fact]
    public async Task LRU_AccessOrder_UpdatesCorrectly()
    {
        // Arrange
        await cache.SetAsync("key1", "response1");
        await cache.SetAsync("key2", "response2");
        await cache.SetAsync("key3", "response3");

        // Act - Access key1 to make it most recently used
        await cache.GetAsync("key1");

        // Fill remaining capacity
        await cache.SetAsync("key4", "response4");
        await cache.SetAsync("key5", "response5");

        // Add one more to trigger eviction
        await cache.SetAsync("key6", "response6");

        // Assert - key2 should be evicted (oldest unused), key1 should remain
        Assert.Null(await cache.GetAsync("key2"));
        Assert.NotNull(await cache.GetAsync("key1"));
        Assert.NotNull(await cache.GetAsync("key6"));
    }

    [Fact]
    public void GetStatistics_ReturnsAccurateData()
    {
        // Arrange
        var initialStats = cache.GetStatistics();

        // Act & Assert initial state
        Assert.Equal(0, initialStats.TotalRequests);
        Assert.Equal(0, initialStats.CacheHits);
        Assert.Equal(0, initialStats.CacheMisses);
        Assert.Equal(0, initialStats.TotalEntries);
    }

    [Fact]
    public async Task GetStatistics_TracksHitsAndMisses()
    {
        // Arrange
        var response = "test response";
        await cache.SetAsync("hit-key", response);

        // Act - Generate hits and misses
        await cache.GetAsync("hit-key"); // Hit
        await cache.GetAsync("hit-key"); // Hit
        await cache.GetAsync("miss-key"); // Miss
        await cache.GetAsync("miss-key"); // Miss

        var stats = cache.GetStatistics();

        // Assert
        Assert.Equal(4, stats.TotalRequests);
        Assert.Equal(2, stats.CacheHits);
        Assert.Equal(2, stats.CacheMisses);
        Assert.Equal(50.0, stats.HitRate);
        Assert.Equal(1, stats.TotalEntries);
    }

    [Fact]
    public async Task GetStatistics_TracksMemoryUsage()
    {
        // Arrange
        var largeResponse = new string('a', 1000) + "|" + new string('b', 1000);

        // Act
        await cache.SetAsync("large-key", largeResponse);
        var stats = cache.GetStatistics();

        // Assert
        Assert.True(stats.MemoryUsageBytes > 2000); // At least the text size
        Assert.Equal(1, stats.TotalEntries);
    }

    [Fact]
    public async Task BackgroundCleanup_RemovesExpiredEntries()
    {
        // Arrange - Create cache with very short TTL for this test
        var shortTtlConfig = new CacheConfiguration
        {
            DefaultTtl = TimeSpan.FromMilliseconds(50),
            EnableBackgroundCleanup = true,
            CleanupInterval = TimeSpan.FromMilliseconds(25)
        };
        using var shortTtlCache = new InMemoryCache(shortTtlConfig);
        
        var shortLivedResponse = "short response";
        var longLivedResponse = "long response";

        // Set short-lived entry first
        await shortTtlCache.SetAsync("short-key", shortLivedResponse);
        
        // Wait a bit, then set long-lived entry with different cache instance that has longer TTL
        await Task.Delay(25);
        await cache.SetAsync("long-key", longLivedResponse);

        // Act - Wait for cleanup to run on short TTL cache
        await Task.Delay(100);

        // Assert
        Assert.Null(await shortTtlCache.GetAsync("short-key"));
        Assert.NotNull(await cache.GetAsync("long-key"));
    }

    [Fact]
    public async Task ConcurrentAccess_ThreadSafe()
    {
        // Arrange - Use cache with higher capacity for this test
        using var largeCache = new InMemoryCache(new CacheConfiguration { MaxCapacity = 20 });
        var tasks = new List<Task>();
        var response = "concurrent response";

        // Act - Multiple concurrent operations
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                await largeCache.SetAsync($"concurrent-key-{index}", $"{response}-{index}");
                await largeCache.GetAsync($"concurrent-key-{index}");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All entries should be accessible
        for (int i = 0; i < 10; i++)
        {
            var result = await largeCache.GetAsync($"concurrent-key-{i}");
            Assert.NotNull(result);
            Assert.Equal($"{response}-{i}", result);
        }
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new InMemoryCache(null));
    }

    [Fact]
    public async Task UpdateEntry_OverwritesExisting()
    {
        // Arrange
        var key = "update-key";
        var originalResponse = "original response";
        var updatedResponse = "updated response";

        // Act
        await cache.SetAsync(key, originalResponse);
        await cache.SetAsync(key, updatedResponse);

        var result = await cache.GetAsync(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(updatedResponse, result);
    }

    [Fact]
    public async Task GetStatistics_TracksUptime()
    {
        // Act
        await Task.Delay(50);
        var stats = cache.GetStatistics();

        // Assert
        Assert.True(stats.Uptime.TotalMilliseconds >= 50);
    }

    public void Dispose()
    {
        cache?.Dispose();
    }
}
