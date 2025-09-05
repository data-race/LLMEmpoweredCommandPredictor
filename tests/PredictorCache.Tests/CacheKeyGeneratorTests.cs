using System;
using System.IO;
using Xunit;
using LLMEmpoweredCommandPredictor.PredictorCache;
using LLMEmpoweredCommandPredictor.Protocol.Models;

namespace LLMEmpoweredCommandPredictor.PredictorCache.Tests;

/// <summary>
/// Comprehensive tests for CacheKeyGenerator to validate key generation logic
/// </summary>
public class CacheKeyGeneratorTests : IDisposable
{
    private readonly CacheKeyGenerator _keyGenerator;
    private readonly string _testDirectory;
    private readonly string _gitTestDirectory;
    private readonly string _nodeTestDirectory;
    private readonly string _dotnetTestDirectory;

    public CacheKeyGeneratorTests()
    {
        _keyGenerator = new CacheKeyGenerator();
        
        // Create temporary test directories
        _testDirectory = Path.Combine(Path.GetTempPath(), "CacheKeyTests", Guid.NewGuid().ToString());
        _gitTestDirectory = Path.Combine(_testDirectory, "GitProject");
        _nodeTestDirectory = Path.Combine(_testDirectory, "NodeProject");
        _dotnetTestDirectory = Path.Combine(_testDirectory, "DotNetProject");
        
        SetupTestDirectories();
    }

    [Fact]
    public void GenerateCacheKey_SameInputSameContext_ReturnsConsistentKey()
    {
        // Arrange
        var request = new SuggestionRequest
        {
            UserInput = "git status",
            WorkingDirectory = _gitTestDirectory
        };

        // Act
        var key1 = _keyGenerator.GenerateCacheKey(request);
        var key2 = _keyGenerator.GenerateCacheKey(request);

        // Assert
        Assert.Equal(key1, key2);
        Assert.NotEmpty(key1);
    }

    [Fact]
    public void GenerateCacheKey_DifferentUserInput_ReturnsDifferentKeys()
    {
        // Arrange
        var request1 = new SuggestionRequest
        {
            UserInput = "git status",
            WorkingDirectory = _gitTestDirectory
        };
        
        var request2 = new SuggestionRequest
        {
            UserInput = "git commit",
            WorkingDirectory = _gitTestDirectory
        };

        // Act
        var key1 = _keyGenerator.GenerateCacheKey(request1);
        var key2 = _keyGenerator.GenerateCacheKey(request2);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_DifferentDirectoryContext_ReturnsDifferentKeys()
    {
        // Arrange
        var gitRequest = new SuggestionRequest
        {
            UserInput = "git",
            WorkingDirectory = _gitTestDirectory
        };
        
        var nodeRequest = new SuggestionRequest
        {
            UserInput = "git",
            WorkingDirectory = _nodeTestDirectory
        };

        // Act
        var gitKey = _keyGenerator.GenerateCacheKey(gitRequest);
        var nodeKey = _keyGenerator.GenerateCacheKey(nodeRequest);

        // Assert
        Assert.NotEqual(gitKey, nodeKey);
    }

    [Fact]
    public void GenerateCacheKey_GitProject_DetectsGitContext()
    {
        // Arrange
        var gitRequest = new SuggestionRequest
        {
            UserInput = "test",
            WorkingDirectory = _gitTestDirectory
        };
        
        var genericRequest = new SuggestionRequest
        {
            UserInput = "test",
            WorkingDirectory = _testDirectory
        };

        // Act
        var gitKey = _keyGenerator.GenerateCacheKey(gitRequest);
        var genericKey = _keyGenerator.GenerateCacheKey(genericRequest);

        // Assert
        Assert.NotEqual(gitKey, genericKey);
    }

    [Fact]
    public void GenerateCacheKey_NodeProject_DetectsNodeContext()
    {
        // Arrange
        var nodeRequest = new SuggestionRequest
        {
            UserInput = "npm",
            WorkingDirectory = _nodeTestDirectory
        };
        
        var genericRequest = new SuggestionRequest
        {
            UserInput = "npm",
            WorkingDirectory = _testDirectory
        };

        // Act
        var nodeKey = _keyGenerator.GenerateCacheKey(nodeRequest);
        var genericKey = _keyGenerator.GenerateCacheKey(genericRequest);

        // Assert
        Assert.NotEqual(nodeKey, genericKey);
    }

    [Fact]
    public void GenerateCacheKey_DotNetProject_DetectsDotNetContext()
    {
        // Arrange
        var dotnetRequest = new SuggestionRequest
        {
            UserInput = "dotnet",
            WorkingDirectory = _dotnetTestDirectory
        };
        
        var genericRequest = new SuggestionRequest
        {
            UserInput = "dotnet",
            WorkingDirectory = _testDirectory
        };

        // Act
        var dotnetKey = _keyGenerator.GenerateCacheKey(dotnetRequest);
        var genericKey = _keyGenerator.GenerateCacheKey(genericRequest);

        // Assert
        Assert.NotEqual(dotnetKey, genericKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void GenerateCacheKey_EmptyOrNullInput_HandlesGracefully(string userInput)
    {
        // Arrange
        var request = new SuggestionRequest
        {
            UserInput = userInput,
            WorkingDirectory = _testDirectory
        };

        // Act & Assert
        var key = _keyGenerator.GenerateCacheKey(request);
        Assert.NotEmpty(key);
    }

    [Fact]
    public void GenerateCacheKey_NonExistentDirectory_HandlesGracefully()
    {
        // Arrange
        var request = new SuggestionRequest
        {
            UserInput = "test",
            WorkingDirectory = "/non/existent/directory"
        };

        // Act & Assert
        var key = _keyGenerator.GenerateCacheKey(request);
        Assert.NotEmpty(key);
    }

    [Fact]
    public void GenerateCacheKey_CaseInsensitiveUserInput_GeneratesSameKey()
    {
        // Arrange
        var request1 = new SuggestionRequest
        {
            UserInput = "Git Status",
            WorkingDirectory = _testDirectory
        };
        
        var request2 = new SuggestionRequest
        {
            UserInput = "git status",
            WorkingDirectory = _testDirectory
        };

        // Act
        var key1 = _keyGenerator.GenerateCacheKey(request1);
        var key2 = _keyGenerator.GenerateCacheKey(request2);

        // Assert
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_WhitespaceInInput_NormalizesCorrectly()
    {
        // Arrange
        var request1 = new SuggestionRequest
        {
            UserInput = "  git status  ",
            WorkingDirectory = _testDirectory
        };
        
        var request2 = new SuggestionRequest
        {
            UserInput = "git status",
            WorkingDirectory = _testDirectory
        };

        // Act
        var key1 = _keyGenerator.GenerateCacheKey(request1);
        var key2 = _keyGenerator.GenerateCacheKey(request2);

        // Assert
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_ReturnsValidHashFormat()
    {
        // Arrange
        var request = new SuggestionRequest
        {
            UserInput = "test command",
            WorkingDirectory = _testDirectory
        };

        // Act
        var key = _keyGenerator.GenerateCacheKey(request);

        // Assert
        Assert.NotEmpty(key);
        Assert.Equal(32, key.Length); // MD5 hash length in hex
        Assert.Matches(@"^[a-f0-9]+$", key); // Only lowercase hex characters
    }

    [Fact]
    public void GenerateCacheKey_MultipleProjectTypes_DetectsCorrectly()
    {
        // Arrange - Create a directory with both Git and Node indicators
        var multiProjectDir = Path.Combine(_testDirectory, "MultiProject");
        Directory.CreateDirectory(multiProjectDir);
        Directory.CreateDirectory(Path.Combine(multiProjectDir, ".git"));
        File.WriteAllText(Path.Combine(multiProjectDir, "package.json"), "{}");

        var multiRequest = new SuggestionRequest
        {
            UserInput = "test",
            WorkingDirectory = multiProjectDir
        };
        
        var gitOnlyRequest = new SuggestionRequest
        {
            UserInput = "test",
            WorkingDirectory = _gitTestDirectory
        };

        // Act
        var multiKey = _keyGenerator.GenerateCacheKey(multiRequest);
        var gitKey = _keyGenerator.GenerateCacheKey(gitOnlyRequest);

        // Assert
        Assert.NotEqual(multiKey, gitKey);
    }

    [Fact]
    public void GenerateCacheKey_PerformanceTest_GeneratesKeysQuickly()
    {
        // Arrange
        var request = new SuggestionRequest
        {
            UserInput = "performance test command",
            WorkingDirectory = _testDirectory
        };

        var startTime = DateTime.UtcNow;

        // Act - Generate 1000 keys
        for (int i = 0; i < 1000; i++)
        {
            _keyGenerator.GenerateCacheKey(request);
        }

        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Should complete in under 1 second
        Assert.True(elapsed.TotalMilliseconds < 1000, $"Key generation took {elapsed.TotalMilliseconds}ms for 1000 keys");
    }

    private void SetupTestDirectories()
    {
        try
        {
            // Create base test directory
            Directory.CreateDirectory(_testDirectory);

            // Setup Git project directory
            Directory.CreateDirectory(_gitTestDirectory);
            Directory.CreateDirectory(Path.Combine(_gitTestDirectory, ".git"));

            // Setup Node project directory  
            Directory.CreateDirectory(_nodeTestDirectory);
            File.WriteAllText(Path.Combine(_nodeTestDirectory, "package.json"), "{}");

            // Setup .NET project directory
            Directory.CreateDirectory(_dotnetTestDirectory);
            File.WriteAllText(Path.Combine(_dotnetTestDirectory, "TestProject.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to setup test directories: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}
