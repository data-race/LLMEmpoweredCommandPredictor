using System;
using System.Collections.Generic;
using LLMEmpoweredCommandPredictor.Protocol.Models;
using Microsoft.Extensions.Logging;

namespace LLMEmpoweredCommandPredictor.PredictorCache;

/// <summary>
/// Generates cache keys from request context for intelligent caching
/// </summary>
public class CacheKeyGenerator
{
    private readonly ILogger<CacheKeyGenerator>? logger;
    private const int maxPrefixLength = 50;

    /// <summary>
    /// Initializes a new instance of the CacheKeyGenerator
    /// </summary>
    /// <param name="logger">Optional logger for debugging</param>
    public CacheKeyGenerator(ILogger<CacheKeyGenerator>? logger = null)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Generates a cache key from the suggestion request and context
    /// </summary>
    public string GenerateCacheKey(SuggestionRequest request)
    {
        logger?.LogDebug("PredictorCache: GenerateCacheKey called for input: {UserInput}", request.UserInput);

        var cacheKey = NormalizeUserInput(request.UserInput);
        
        logger?.LogDebug("PredictorCache: Generated cache key: {CacheKey} for input: {UserInput}", 
            cacheKey, request.UserInput);

        return cacheKey;
    }

    /// <summary>
    /// Generates multiple cache keys for prefix matching (1 to maxPrefixLength character prefixes)
    /// This allows "g" to match cached results for "git branch", "git status", etc.
    /// </summary>
    public List<string> GenerateAllPrefixKeys(SuggestionRequest request)
    {
        var keys = new List<string>();
        var normalizedInput = NormalizeUserInput(request.UserInput);
        
        if (string.IsNullOrEmpty(normalizedInput))
            return keys;

        // Generate prefix keys from 1 character up to maxPrefixLength
        var maxLength = Math.Min(maxPrefixLength, normalizedInput.Length);
        
        for (int i = 1; i <= maxLength; i++)
        {
            keys.Add(normalizedInput.Substring(0, i));
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
        var maxLength = Math.Min(maxPrefixLength, normalizedInput.Length);
        
        for (int i = maxLength; i >= 1; i--)
        {
            keys.Add(normalizedInput.Substring(0, i));
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

}
