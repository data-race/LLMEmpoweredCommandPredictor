using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Subsystem.Prediction;
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
    public async Task SetAsync_AndGetAsync_ReturnsStoredSuggestions()
    {
        // Arrange
        var suggestions = CreateTestSuggestions("test1", "test2");
        var key = "test-key";
        var ttl = TimeSpan.FromMinutes(10);

        // Act
        await cache.SetAsync(key, suggestions, ttl);
        var result = await cache.GetAsync(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("test1", result[0].SuggestionText);
        Assert.Equal("test2", result[1].SuggestionText);
    }

    [Fact]
    public async Task SetAsync_WithNullSuggestions_DoesNotStore()
    {
        // Act
        await cache.SetAsync("key", null, TimeSpan.FromMinutes(10));
        var result = await cache.GetAsync("key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_WithEmptySuggestions_DoesNotStore()
    {
        // Act
        await cache.SetAsync("key", new List<PredictiveSuggestion>(), TimeSpan.FromMinutes(10));
        var result = await cache.GetAsync("key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WithExpiredEntry_ReturnsNull()
    {
        // Arrange
        var suggestions = CreateTestSuggestions("test");
        var key = "expired-key";
        var shortTtl = TimeSpan.FromMilliseconds(50);

        // Act
        await cache.SetAsync(key, suggestions, shortTtl);
        await Task.Delay(100); // Wait for expiration
        var result = await cache.GetAsync(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_RemovesEntry()
    {
        // Arrange
        var suggestions = CreateTestSuggestions("test");
        var key = "remove-key";

        await cache.SetAsync(key, suggestions, TimeSpan.FromMinutes(10));
        
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
        await cache.SetAsync("key1", CreateTestSuggestions("test1"), TimeSpan.FromMinutes(10));
        await cache.SetAsync("key2", CreateTestSuggestions("test2"), TimeSpan.FromMinutes(10));

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
            await cache.SetAsync($"key{i}", CreateTestSuggestions($"test{i}"), TimeSpan.FromMinutes(10));
        }

        // Act - Add one more entry to trigger eviction
        await cache.SetAsync("new-key", CreateTestSuggestions("new-test"), TimeSpan.FromMinutes(10));

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
        await cache.SetAsync("key1", CreateTestSuggestions("test1"), TimeSpan.FromMinutes(10));
        await cache.SetAsync("key2", CreateTestSuggestions("test2"), TimeSpan.FromMinutes(10));
        await cache.SetAsync("key3", CreateTestSuggestions("test3"), TimeSpan.FromMinutes(10));

        // Act - Access key1 to make it most recently used
        await cache.GetAsync("key1");

        // Fill remaining capacity
        await cache.SetAsync("key4", CreateTestSuggestions("test4"), TimeSpan.FromMinutes(10));
        await cache.SetAsync("key5", CreateTestSuggestions("test5"), TimeSpan.FromMinutes(10));

        // Add one more to trigger eviction
        await cache.SetAsync("key6", CreateTestSuggestions("test6"), TimeSpan.FromMinutes(10));

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
        var suggestions = CreateTestSuggestions("test");
        await cache.SetAsync("hit-key", suggestions, TimeSpan.FromMinutes(10));

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
        var largeSuggestions = CreateTestSuggestions(
            new string('a', 1000), 
            new string('b', 1000)
        );

        // Act
        await cache.SetAsync("large-key", largeSuggestions, TimeSpan.FromMinutes(10));
        var stats = cache.GetStatistics();

        // Assert
        Assert.True(stats.MemoryUsageBytes > 2000); // At least the text size
        Assert.Equal(1, stats.TotalEntries);
    }

    [Fact]
    public async Task BackgroundCleanup_RemovesExpiredEntries()
    {
        // Arrange
        var shortLivedSuggestions = CreateTestSuggestions("short");
        var longLivedSuggestions = CreateTestSuggestions("long");

        await cache.SetAsync("short-key", shortLivedSuggestions, TimeSpan.FromMilliseconds(50));
        await cache.SetAsync("long-key", longLivedSuggestions, TimeSpan.FromMinutes(10));

        // Act - Wait for cleanup to run
        await Task.Delay(200);

        // Assert
        Assert.Null(await cache.GetAsync("short-key"));
        Assert.NotNull(await cache.GetAsync("long-key"));
    }

    [Fact]
    public async Task ConcurrentAccess_ThreadSafe()
    {
        // Arrange - Use cache with higher capacity for this test
        using var largeCache = new InMemoryCache(new CacheConfiguration { MaxCapacity = 20 });
        var tasks = new List<Task>();
        var suggestions = CreateTestSuggestions("concurrent");

        // Act - Multiple concurrent operations
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                await largeCache.SetAsync($"concurrent-key-{index}", suggestions, TimeSpan.FromMinutes(10));
                await largeCache.GetAsync($"concurrent-key-{index}");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All entries should be accessible
        for (int i = 0; i < 10; i++)
        {
            var result = await largeCache.GetAsync($"concurrent-key-{i}");
            Assert.NotNull(result);
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
        var originalSuggestions = CreateTestSuggestions("original");
        var updatedSuggestions = CreateTestSuggestions("updated");

        // Act
        await cache.SetAsync(key, originalSuggestions, TimeSpan.FromMinutes(10));
        await cache.SetAsync(key, updatedSuggestions, TimeSpan.FromMinutes(10));

        var result = await cache.GetAsync(key);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("updated", result[0].SuggestionText);
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

    private static IReadOnlyList<PredictiveSuggestion> CreateTestSuggestions(params string[] texts)
    {
        return texts.Select(text => new PredictiveSuggestion(text)).ToList().AsReadOnly();
    }

    public void Dispose()
    {
        cache?.Dispose();
    }
}
