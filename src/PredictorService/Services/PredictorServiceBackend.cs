using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LLMEmpoweredCommandPredictor.Protocol.Integration;
using LLMEmpoweredCommandPredictor.Protocol.Models;
using LLMEmpoweredCommandPredictor.Protocol.Contracts;
using LLMEmpoweredCommandPredictor.PredictorService.Context;
using LLMEmpoweredCommandPredictor.PredictorCache;
using System.Management.Automation.Subsystem.Prediction;
using System.Text.Json;

namespace LLMEmpoweredCommandPredictor.PredictorService.Services;

public class PredictorServiceBackend : ISuggestionService, IDisposable
{
    private readonly ILogger<PredictorServiceBackend> _logger;
    private readonly ContextManager _contextManager;
    private readonly AzureOpenAIService? _azureOpenAIService;
    private readonly PromptTemplateService? _promptTemplateService;
    private readonly InMemoryCache _cache;
    private readonly CacheKeyGenerator _keyGenerator;

    public PredictorServiceBackend(
        ILogger<PredictorServiceBackend> logger,
        ContextManager contextManager,
        InMemoryCache cache,
        CacheKeyGenerator keyGenerator,
        AzureOpenAIService? azureOpenAIService = null,
        PromptTemplateService? promptTemplateService = null)
    {
        _logger = logger;
        _contextManager = contextManager;
        _cache = cache;
        _keyGenerator = keyGenerator;
        _azureOpenAIService = azureOpenAIService;
        _promptTemplateService = promptTemplateService;
        
        _logger.LogInformation("PredictorServiceBackend: Initialized with cache support - cache only mode");
    }


    /// <summary>
    /// Creates a SuggestionRequest from context for cache operations
    /// </summary>
    private SuggestionRequest CreateSuggestionRequestFromContext(object context, int maxSuggestions)
    {
        try
        {
            // Handle SuggestionRequest directly
            if (context is SuggestionRequest request)
            {
                return request;
            }

            // Try to extract UserInput from context
            var userInput = GetUserInputFromContext(context);
            return new SuggestionRequest(userInput, maxSuggestions: maxSuggestions);
        }
        catch
        {
            return new SuggestionRequest("", maxSuggestions: maxSuggestions);
        }
    }

    /// <summary>
    /// Extracts user input from the context object
    /// </summary>
    private string GetUserInputFromContext(object context)
    {
        try
        {
            // Try to get UserInput property via reflection
            var userInputProperty = context.GetType().GetProperty("UserInput");
            if (userInputProperty != null)
            {
                return userInputProperty.GetValue(context)?.ToString() ?? "";
            }

            // Fallback to string representation
            return context.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }


    #region ISuggestionService Implementation
    
    /// <inheritdoc />
    public async Task<SuggestionResponse> GetSuggestionsAsync(
        SuggestionRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return new SuggestionResponse(
                suggestions: new List<PredictiveSuggestion>(),
                source: "error",
                warningMessage: "Invalid request"
            );
        }

        try
        {
            _logger.LogInformation("PredictorServiceBackend: GetSuggestionsAsync called for input: '{UserInput}'", request.UserInput);
            
            // 1. Generate cache key
            var cacheKey = _keyGenerator.GenerateCacheKey(request);
            _logger.LogDebug("PredictorServiceBackend: Generated cache key: '{CacheKey}'", cacheKey);
            
            // 2. Check cache ONLY - do not generate suggestions
            var cachedResponse = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cachedResponse != null)
            {
                try
                {
                    // Try to parse the simplified JSON format
                    var cachedSuggestionResponse = ParseCachedResponse(cachedResponse);
                    if (cachedSuggestionResponse != null)
                    {
                        // Update cache metadata
                        cachedSuggestionResponse.IsFromCache = true;
                        cachedSuggestionResponse.CachedTimestamp = DateTime.UtcNow;
                        cachedSuggestionResponse.GenerationTimeMs = 1.0; // Very fast cache retrieval
                        
                        _logger.LogInformation("PredictorServiceBackend: Cache HIT for '{UserInput}', returning cached response with {Count} suggestions", 
                            request.UserInput, cachedSuggestionResponse.Suggestions.Count);
                        return cachedSuggestionResponse;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("PredictorServiceBackend: Failed to deserialize cached response: {Error}", ex.Message);
                    // Remove corrupted cache entry
                    await _cache.RemoveAsync(cacheKey, cancellationToken);
                }
            }

            _logger.LogInformation("PredictorServiceBackend: Cache MISS for '{UserInput}', returning empty suggestions", request.UserInput);

            // 3. Cache miss - return empty suggestions (no generation)
            return new SuggestionResponse(
                suggestions: new List<PredictiveSuggestion>(),
                source: "cache-only",
                warningMessage: "No cached suggestions available"
            );
        }
        catch (OperationCanceledException)
        {
            return new SuggestionResponse(
                suggestions: new List<PredictiveSuggestion>(),
                source: "cancelled",
                warningMessage: "Request was cancelled"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PredictorServiceBackend: Error getting suggestions");
            return new SuggestionResponse(
                suggestions: new List<PredictiveSuggestion>(),
                source: "error",
                warningMessage: $"Service error: {ex.Message}"
            );
        }
    }

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.FromResult(true);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.ClearAsync(cancellationToken);
            _logger.LogInformation("PredictorServiceBackend: Cache cleared");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PredictorServiceBackend: Failed to clear cache: {Error}", ex.Message);
        }
    }

    /// <inheritdoc />
    Task<ServiceStatus> ISuggestionService.GetStatusAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new ServiceStatus(
            isRunning: true,
            uptime: TimeSpan.FromMilliseconds(Environment.TickCount64),
            cachedSuggestionsCount: _cache.GetStatistics().TotalEntries,
            totalRequestsProcessed: _cache.GetStatistics().TotalRequests,
            averageResponseTimeMs: _cache.GetStatistics().CacheHits > 0 ? 5.0 : 150.0,
            lastCacheUpdate: _cache.GetStatistics().LastAccess,
            version: "1.0.0",
            memoryUsageMb: _cache.GetStatistics().MemoryUsageBytes / (1024.0 * 1024.0)
        ));
    }

    /// <inheritdoc />
    public async Task TriggerCacheRefreshAsync(
        SuggestionRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Cache-only mode - no refresh capability
        _logger.LogInformation("PredictorServiceBackend: Cache refresh requested but running in cache-only mode");
        await Task.CompletedTask;
    }

    #endregion

    /// <summary>
    /// Parses the simplified cached JSON format and converts it to SuggestionResponse.
    /// Returns ALL suggestions from the cached array.
    /// </summary>
    private SuggestionResponse? ParseCachedResponse(string cachedJson)
    {
        try
        {
            // Parse the simplified JSON format: {"Suggestions":["cmd1","cmd2"],"Source":"...","IsFromCache":false,"GenerationTimeMs":1.0}
            using var document = JsonDocument.Parse(cachedJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("Suggestions", out var suggestionsElement))
                return null;

            var suggestions = new List<PredictiveSuggestion>();
            
            // Parse ALL suggestions from the array
            foreach (var suggestionElement in suggestionsElement.EnumerateArray())
            {
                var suggestionText = suggestionElement.GetString();
                if (!string.IsNullOrEmpty(suggestionText))
                {
                    suggestions.Add(new PredictiveSuggestion(suggestionText));
                }
            }

            // If no valid suggestions found, return null
            if (suggestions.Count == 0)
            {
                _logger.LogWarning("PredictorServiceBackend: No valid suggestions found in cached response");
                return null;
            }

            _logger.LogDebug("PredictorServiceBackend: Parsed {Count} suggestions from cache", suggestions.Count);

            // Extract other properties if they exist
            var source = root.TryGetProperty("Source", out var sourceElement) ? sourceElement.GetString() ?? "cache" : "cache";
            var generationTimeMs = root.TryGetProperty("GenerationTimeMs", out var timeElement) ? timeElement.GetDouble() : 1.0;

            return new SuggestionResponse(
                suggestions: suggestions,
                source: source,
                generationTimeMs: generationTimeMs,
                isFromCache: false, // Will be set to true by caller
                warningMessage: null
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PredictorServiceBackend: Error parsing cached response: {Error}", ex.Message);
            return null;
        }
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
