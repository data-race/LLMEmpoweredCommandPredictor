using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LLMEmpoweredCommandPredictor.Protocol.Models;

namespace LLMEmpoweredCommandPredictor.PredictorCache;

/// <summary>
/// Generates cache keys from request context for intelligent caching
/// </summary>
public class CacheKeyGenerator
{
    /// <summary>
    /// Generates a cache key from the suggestion request and context
    /// </summary>
    public string GenerateCacheKey(SuggestionRequest request)
    {
        var keyComponents = new CacheKeyComponents
        {
            UserInput = NormalizeUserInput(request.UserInput),
            WorkingDirectory = GetDirectorySignature(request.WorkingDirectory),
            DirectoryContext = GetDirectoryContext(request.WorkingDirectory),
            TimeContext = GetTimeContext(),
            SessionContext = GetSessionContext()
        };

        return GenerateHashFromComponents(keyComponents);
    }

    /// <summary>
    /// Generates multiple cache keys for prefix matching (1-3 character prefixes)
    /// This allows "g" to match cached results for "git branch", "git status", etc.
    /// </summary>
    public List<string> GenerateAllPrefixKeys(SuggestionRequest request)
    {
        var keys = new List<string>();
        var normalizedInput = NormalizeUserInput(request.UserInput);
        
        if (string.IsNullOrEmpty(normalizedInput))
            return keys;

        // Generate prefix keys for 1, 2, 3 characters
        var maxLength = Math.Min(3, normalizedInput.Length);
        
        for (int i = 1; i <= maxLength; i++)
        {
            var prefix = normalizedInput.Substring(0, i);
            var prefixRequest = new SuggestionRequest(
                userInput: prefix,
                workingDirectory: request.WorkingDirectory,
                maxSuggestions: request.MaxSuggestions,
                commandHistory: request.CommandHistory,
                powerShellVersion: request.PowerShellVersion,
                operatingSystem: request.OperatingSystem,
                userSessionId: request.UserSessionId,
                priority: request.Priority
            );
            
            keys.Add(GenerateCacheKey(prefixRequest));
        }

        return keys;
    }

    /// <summary>
    /// Finds matching prefix cache keys for a given input (longest to shortest)
    /// </summary>
    public List<string> FindMatchingPrefixKeys(SuggestionRequest request)
    {
        var keys = new List<string>();
        var normalizedInput = NormalizeUserInput(request.UserInput);
        
        if (string.IsNullOrEmpty(normalizedInput))
            return keys;

        // Search from longest possible prefix down to single character
        var maxLength = Math.Min(3, normalizedInput.Length);
        
        for (int i = maxLength; i >= 1; i--)
        {
            var prefix = normalizedInput.Substring(0, i);
            var prefixRequest = new SuggestionRequest(
                userInput: prefix,
                workingDirectory: request.WorkingDirectory,
                maxSuggestions: request.MaxSuggestions,
                commandHistory: request.CommandHistory,
                powerShellVersion: request.PowerShellVersion,
                operatingSystem: request.OperatingSystem,
                userSessionId: request.UserSessionId,
                priority: request.Priority
            );
            
            keys.Add(GenerateCacheKey(prefixRequest));
        }

        return keys;
    }

    /// <summary>
    /// Normalizes user input for consistent cache keys
    /// </summary>
    private string NormalizeUserInput(string userInput)
    {
        return userInput?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Gets a signature for the working directory (not full path for privacy)
    /// </summary>
    private string GetDirectorySignature(string workingDirectory)
    {
        if (string.IsNullOrEmpty(workingDirectory))
            return "unknown";

        try
        {
            // Use directory name + parent name for context, not full path
            var dirInfo = new DirectoryInfo(workingDirectory);
            var dirName = dirInfo.Name;
            var parentName = dirInfo.Parent?.Name ?? "root";
            
            return $"{parentName}_{dirName}";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Analyzes directory contents for project type indicators
    /// </summary>
    private string GetDirectoryContext(string workingDirectory)
    {
        if (string.IsNullOrEmpty(workingDirectory) || !Directory.Exists(workingDirectory))
            return "unknown";

        var indicators = new List<string>();

        try
        {
            // Check for common project files/folders
            var projectIndicators = new Dictionary<string, string>
            {
                { ".git", "git" },
                { "package.json", "node" },
                { "Dockerfile", "docker" },
                { "requirements.txt", "python" },
                { "pom.xml", "maven" },
                { "Cargo.toml", "rust" },
                { "go.mod", "go" }
            };

            foreach (var (file, indicator) in projectIndicators)
            {
                if (File.Exists(Path.Combine(workingDirectory, file)) || 
                    Directory.Exists(Path.Combine(workingDirectory, file)))
                {
                    indicators.Add(indicator);
                }
            }

            // Check for .NET projects
            if (Directory.GetFiles(workingDirectory, "*.csproj").Any() ||
                Directory.GetFiles(workingDirectory, "*.sln").Any())
            {
                indicators.Add("dotnet");
            }

            return indicators.Any() ? string.Join(",", indicators.OrderBy(x => x)) : "generic";
        }
        catch
        {
            return "generic";
        }
    }

    /// <summary>
    /// Gets time-based context for cache relevancy
    /// </summary>
    private string GetTimeContext()
    {
        var now = DateTime.Now;
        // Group by hour to allow cache hits within same working session
        return $"{now.Year}{now.Month:D2}{now.Day:D2}_{now.Hour:D2}";
    }

    /// <summary>
    /// Gets session context (simplified for now)
    /// </summary>
    private string GetSessionContext()
    {
        // For now, use process start time as session identifier
        var sessionId = Environment.ProcessId.ToString();
        return $"session_{sessionId}";
    }

    /// <summary>
    /// Generates MD5 hash from cache key components
    /// </summary>
    private string GenerateHashFromComponents(CacheKeyComponents components)
    {
        var keyString = JsonSerializer.Serialize(components, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(keyString));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

/// <summary>
/// Components used to generate cache keys
/// </summary>
internal class CacheKeyComponents
{
    public string UserInput { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string DirectoryContext { get; set; } = string.Empty;
    public string TimeContext { get; set; } = string.Empty;
    public string SessionContext { get; set; } = string.Empty;
}
