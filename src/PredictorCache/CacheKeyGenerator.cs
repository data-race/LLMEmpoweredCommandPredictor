using System;
using LLMEmpoweredCommandPredictor.Protocol.Models;

namespace LLMEmpoweredCommandPredictor.PredictorCache;

/// <summary>
/// Simplified cache key generator that uses only user input for history-based prediction
/// </summary>
public class CacheKeyGenerator
{
    /// <summary>
    /// Generates a simple cache key from the user input only
    /// </summary>
    /// <param name="request">The suggestion request</param>
    /// <returns>Normalized user input as cache key</returns>
    public string GenerateCacheKey(SuggestionRequest request)
    {
        return NormalizeUserInput(request.UserInput);
    }

    /// <summary>
    /// Normalizes user input for consistent cache keys
    /// </summary>
    /// <param name="userInput">The user input to normalize</param>
    /// <returns>Normalized user input (trimmed and lowercase)</returns>
    private string NormalizeUserInput(string userInput)
    {
        return userInput?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
