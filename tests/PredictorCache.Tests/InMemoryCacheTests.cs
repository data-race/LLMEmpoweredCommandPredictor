using System;
using System.Threading.Tasks;
using Xunit;
using LLMEmpoweredCommandPredictor.PredictorCache;

namespace LLMEmpoweredCommandPredictor.PredictorCache.Tests;

/// <summary>
/// Tests for the InMemoryCache implementation
/// </summary>
public class InMemoryCacheTests : IDisposable
{
    private readonly InMemoryCache _cache;

    public InMemoryCacheTests()
    {
        _cache = new InMemoryCache();
    }

    [Fact]
    public async Task GetAsync_EmptyCache_ReturnsNull()
    {
        // Act
        var result = await _cache.GetAsync("git");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_SingleCommand_StoresForAllPrefixes()
    {
        // Arrange
        var command = "git branch";

        // Act
        await _cache.SetAsync(command, "unused");

        // Assert - Check that prefixes are created
        var gResult = await _cache.GetAsync("g");
        var gitResult = await _cache.GetAsync("git");
        var gitSpaceResult = await _cache.GetAsync("git ");

        Assert.Equal("git branch", gResult);
        Assert.Equal("git branch", gitResult);
        Assert.Equal("git branch", gitSpaceResult);
    }

    [Fact]
    public async Task GetAsync_MultipleCommands_ReturnsAllMatching()
    {
        // Arrange
        await _cache.SetAsync("git branch", "unused");
        await _cache.SetAsync("git pull", "unused");
        await _cache.SetAsync("git status", "unused");

        // Act
        var result = await _cache.GetAsync("git");

        // Assert
        var commands = result?.Split('\n');
        Assert.NotNull(commands);
        Assert.Equal(3, commands.Length);
        Assert.Contains("git branch", commands);
        Assert.Contains("git pull", commands);
        Assert.Contains("git status", commands);
    }

    [Fact]
    public async Task GetAsync_PrefixMatch_ReturnsCorrectCommands()
    {
        // Arrange
        await _cache.SetAsync("git branch", "unused");
        await _cache.SetAsync("git pull", "unused");
        await _cache.SetAsync("ls", "unused");

        // Act
        var gitResult = await _cache.GetAsync("git p");
        var lsResult = await _cache.GetAsync("l");

        // Assert
        Assert.Equal("git pull", gitResult);
        Assert.Equal("ls", lsResult);
    }

    [Fact]
    public async Task SetAsync_DuplicateCommand_IncreasesExecutionCount()
    {
        // Arrange
        var command = "git status";

        // Act
        await _cache.SetAsync(command, "unused");
        await _cache.SetAsync(command, "unused");
        await _cache.SetAsync(command, "unused");

        // Get internal data for verification
        var cachedCommands = _cache.GetAllCachedCommands();

        // Assert
        Assert.True(cachedCommands.ContainsKey("git"));
        var gitCommands = cachedCommands["git"];
        Assert.Contains("git status", gitCommands);
        
        // The command should appear only once (not duplicated)
        Assert.Single(gitCommands);
    }

    [Fact]
    public async Task GetAsync_OrdersByFrequencyAndRecency()
    {
        // Arrange
        await _cache.SetAsync("git branch", "unused");
        await _cache.SetAsync("git pull", "unused");
        await _cache.SetAsync("git status", "unused");
        
        // Execute git pull multiple times to increase frequency
        await _cache.SetAsync("git pull", "unused");
        await _cache.SetAsync("git pull", "unused");

        // Act
        var result = await _cache.GetAsync("git");

        // Assert
        var commands = result?.Split('\n');
        Assert.NotNull(commands);
        
        // git pull should be first due to higher frequency
        Assert.Equal("git pull", commands[0]);
    }

    [Fact]
    public async Task RemoveAsync_RemovesCommandFromAllPrefixes()
    {
        // Arrange
        await _cache.SetAsync("git branch", "unused");
        await _cache.SetAsync("git pull", "unused");

        // Act
        await _cache.RemoveAsync("git branch");

        // Assert
        var gitResult = await _cache.GetAsync("git");
        var gitBResult = await _cache.GetAsync("git b");

        Assert.Equal("git pull", gitResult);
        Assert.Null(gitBResult);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllEntries()
    {
        // Arrange
        await _cache.SetAsync("git branch", "unused");
        await _cache.SetAsync("git pull", "unused");
        await _cache.SetAsync("ls", "unused");

        // Act
        await _cache.ClearAsync();

        // Assert
        var gitResult = await _cache.GetAsync("git");
        var lsResult = await _cache.GetAsync("l");

        Assert.Null(gitResult);
        Assert.Null(lsResult);
    }

    [Fact]
    public async Task GetStatistics_TracksUsageCorrectly()
    {
        // Arrange
        await _cache.SetAsync("git branch", "unused");

        // Act
        await _cache.GetAsync("git"); // Hit
        await _cache.GetAsync("nonexistent"); // Miss

        var stats = _cache.GetStatistics();

        // Assert
        Assert.Equal(2, stats.TotalRequests);
        Assert.Equal(1, stats.CacheHits);
        Assert.Equal(1, stats.CacheMisses);
        Assert.Equal(50.0, stats.HitRate);
    }

    [Theory]
    [InlineData("Git Branch")]
    [InlineData("  git branch  ")]
    [InlineData("GIT BRANCH")]
    public async Task GetAsync_CaseInsensitive_FindsMatch(string input)
    {
        // Arrange
        await _cache.SetAsync("git branch", "unused");

        // Act
        var result = await _cache.GetAsync(input);

        // Assert
        Assert.Equal("git branch", result);
    }

    [Fact]
    public async Task SetAsync_EmptyOrNullInput_HandlesGracefully()
    {
        // Act & Assert
        await _cache.SetAsync("", "unused");
        await _cache.SetAsync(null!, "unused");

        var stats = _cache.GetStatistics();
        Assert.Equal(0, stats.TotalEntries);
    }

    [Fact]
    public async Task GetAsync_EmptyOrNullInput_ReturnsNull()
    {
        // Act & Assert
        var result1 = await _cache.GetAsync("");
        var result2 = await _cache.GetAsync(null!);

        Assert.Null(result1);
        Assert.Null(result2);
    }

    [Fact]
    public async Task SetAsync_LongCommand_RespectsPrefixLimit()
    {
        // Arrange
        var longCommand = new string('a', 100); // 100 character command
        var cache = new InMemoryCache(maxPrefixLength: 20);

        // Act
        await cache.SetAsync(longCommand, "unused");

        // Assert
        var cachedCommands = cache.GetAllCachedCommands();
        
        // Should not have prefixes longer than 20 characters
        var longestPrefix = cachedCommands.Keys.Max(k => k.Length);
        Assert.True(longestPrefix <= 20);

        cache.Dispose();
    }

    [Fact]
    public async Task SetAsync_ManyCommands_RespectsCommandLimit()
    {
        // Arrange
        var cache = new InMemoryCache(maxCommandsPerPrefix: 3);

        // Act - Add 5 commands with same prefix
        await cache.SetAsync("git branch", "unused");
        await cache.SetAsync("git pull", "unused");
        await cache.SetAsync("git status", "unused");
        await cache.SetAsync("git add", "unused");
        await cache.SetAsync("git commit", "unused");

        // Assert
        var result = await cache.GetAsync("git");
        var commands = result?.Split('\n');
        
        Assert.NotNull(commands);
        Assert.True(commands.Length <= 3); // Should respect limit

        cache.Dispose();
    }

    [Fact]
    public async Task RealWorldScenario_GitWorkflow_WorksCorrectly()
    {
        // Arrange - Simulate a typical git workflow
        await _cache.SetAsync("git status", "unused");
        await _cache.SetAsync("git add .", "unused");
        await _cache.SetAsync("git commit -m 'fix'", "unused");
        await _cache.SetAsync("git push", "unused");
        await _cache.SetAsync("git pull", "unused");
        await _cache.SetAsync("git branch", "unused");

        // Act & Assert
        var gResult = await _cache.GetAsync("g");
        Assert.Contains("git", gResult);

        var gitResult = await _cache.GetAsync("git");
        var gitCommands = gitResult?.Split('\n');
        Assert.NotNull(gitCommands);
        Assert.Equal(6, gitCommands.Length);

        var gitPResult = await _cache.GetAsync("git p");
        var gitPCommands = gitPResult?.Split('\n');
        Assert.NotNull(gitPCommands);
        Assert.Contains("git push", gitPCommands);
        Assert.Contains("git pull", gitPCommands);
        Assert.Equal(2, gitPCommands.Length);

        var gitAddResult = await _cache.GetAsync("git a");
        Assert.Equal("git add .", gitAddResult);
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }
}
