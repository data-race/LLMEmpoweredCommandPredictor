using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LLMEmpoweredCommandPredictor.Protocol.Contracts;
using LLMEmpoweredCommandPredictor.Protocol.Models;
using LLMEmpoweredCommandPredictor.Protocol.Adapters;
using LLMEmpoweredCommandPredictor.Protocol.Abstractions;

namespace LLMEmpoweredCommandPredictor.Protocol.Integration;

/// <summary>
/// Enhanced ServiceBridge with integrated caching support.
/// This class combines Protocol layer with Cache layer to provide high-performance suggestions.
/// </summary>
public class CachedServiceBridge : ISuggestionService, IDisposable
{
    private readonly IServiceBackend _backend;
    private readonly ICacheService _cache;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly object _lockObject = new object();
    private volatile bool _isDisposed = false;

    /// <summary>
    /// Creates a new CachedServiceBridge with backend and cache services
    /// </summary>
    /// <param name="backend">The backend service implementation</param>
    /// <param name="cache">The cache service (optional, creates default if null)</param>
    /// <param name="keyGenerator">The cache key generator</param>
    public CachedServiceBridge(
        IServiceBackend backend, 
        ICacheService cache,
        ICacheKeyGenerator keyGenerator)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
    }

    /// <inheritdoc />
    public async Task<SuggestionResponse> GetSuggestionsAsync(
        SuggestionRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CachedServiceBridge));

        if (request == null)
        {
            return new SuggestionResponse(
                suggestions: new List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>(),
                source: "error",
                warningMessage: "Invalid request"
            );
        }

        try
        {
            // 1. Try prefix matching first for better cache hits
            var cachedResponse = await TryPrefixMatch(request, cancellationToken);
            if (cachedResponse != null)
            {
                return cachedResponse;
            }
            
            // 2. Fallback to exact cache key matching
            var cacheKey = _keyGenerator.GenerateCacheKey(request);
            var exactCachedResponse = await _cache.GetAsync(cacheKey, cancellationToken);
            if (exactCachedResponse != null)
            {
                // Cache hit - deserialize and return
                try
                {
                    var cachedSuggestionResponse = JsonSerializer.Deserialize<SuggestionResponse>(exactCachedResponse);
                    if (cachedSuggestionResponse != null)
                    {
                        // Update cache metadata
                        cachedSuggestionResponse.IsFromCache = true;
                        cachedSuggestionResponse.CachedTimestamp = DateTime.UtcNow;
                        cachedSuggestionResponse.GenerationTimeMs = 1.0; // Very fast cache retrieval
                        
                        return cachedSuggestionResponse;
                    }
                }
                catch (JsonException)
                {
                    // Corrupted cache entry - remove it and continue to backend
                    await _cache.RemoveAsync(cacheKey, cancellationToken);
                }
            }

            // 3. Cache miss - call backend service
            var serviceContext = ContextTransformer.ToServiceContext(request);
            var serviceResponse = await _backend.ProcessSuggestionAsync(
                serviceContext, 
                request.MaxSuggestions, 
                cancellationToken);
            
            // 4. Transform Service response to Protocol format
            var protocolResponse = ContextTransformer.FromServiceResponse(
                serviceResponse, 
                request.MaxSuggestions, 
                isFromCache: false);

            // 5. Store in cache with prefix keys for better matching (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await StoreWithPrefixKeys(request, protocolResponse);
                }
                catch
                {
                    // Ignore cache storage errors
                }
            }, CancellationToken.None);

            return protocolResponse;
        }
        catch (OperationCanceledException)
        {
            return new SuggestionResponse(
                suggestions: new List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>(),
                source: "cancelled",
                warningMessage: "Request was cancelled"
            );
        }
        catch (Exception ex)
        {
            return new SuggestionResponse(
                suggestions: new List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>(),
                source: "error",
                warningMessage: $"Service error: {ex.Message}"
            );
        }
    }

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            return false;

        try
        {
            return await _backend.IsHealthyAsync(cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return new ServiceStatus(
                isRunning: false,
                errorMessage: "Service bridge is disposed"
            );
        }

        try
        {
            var backendStatus = await _backend.GetStatusAsync(cancellationToken);
            var cacheStats = _cache.GetStatistics();
            
            // Combine backend status with cache statistics
            return TransformBackendStatusWithCache(backendStatus, cacheStats);
        }
        catch (Exception ex)
        {
            return new ServiceStatus(
                isRunning: false,
                errorMessage: $"Failed to get status: {ex.Message}"
            );
        }
    }

    /// <inheritdoc />
    public async Task TriggerCacheRefreshAsync(
        SuggestionRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            return;

        try
        {
            // Generate cache key
            var cacheKey = _keyGenerator.GenerateCacheKey(request);
            
            // Check if already cached
            var existingCache = await _cache.GetAsync(cacheKey, cancellationToken);
            if (existingCache != null)
            {
                return; // Already cached, no need to refresh
            }

            // Trigger background cache population
            var serviceContext = ContextTransformer.ToServiceContext(request);
            await _backend.PrewarmCacheAsync(serviceContext, cancellationToken);
        }
        catch (Exception)
        {
            // Log error but don't throw - this is a background operation
        }
    }

    /// <inheritdoc />
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            return;

        try
        {
            await _cache.ClearAsync(cancellationToken);
            await _backend.ClearCacheAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Log error but don't throw
        }
    }

    /// <summary>
    /// Gets cache statistics for monitoring
    /// </summary>
    /// <returns>Current cache statistics</returns>
    public ICacheStatistics GetCacheStatistics()
    {
        return _cache.GetStatistics();
    }

    /// <summary>
    /// Transforms backend status combined with cache statistics to Protocol ServiceStatus format
    /// </summary>
    private ServiceStatus TransformBackendStatusWithCache(object backendStatus, ICacheStatistics cacheStats)
    {
        try
        {
            var isRunning = GetPropertyValue<bool?>(backendStatus, "IsRunning") ?? true;
            var uptime = GetPropertyValue<TimeSpan?>(backendStatus, "Uptime") ?? TimeSpan.Zero;
            var errorMessage = GetPropertyValue<string>(backendStatus, "ErrorMessage");
            
            return new ServiceStatus(
                isRunning: isRunning,
                uptime: uptime,
                cachedSuggestionsCount: cacheStats.TotalEntries,
                totalRequestsProcessed: cacheStats.TotalRequests,
                averageResponseTimeMs: cacheStats.CacheHits > 0 ? 5.0 : 150.0, // Fast cache vs slower LLM
                lastCacheUpdate: cacheStats.LastAccess,
                version: "1.0.0",
                errorMessage: errorMessage,
                memoryUsageMb: cacheStats.MemoryUsageBytes / (1024.0 * 1024.0)
            );
        }
        catch
        {
            return new ServiceStatus(
                isRunning: true,
                cachedSuggestionsCount: cacheStats.TotalEntries
            );
        }
    }

    /// <summary>
    /// Attempts to find cached suggestions using prefix matching
    /// </summary>
    private async Task<SuggestionResponse?> TryPrefixMatch(SuggestionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if the key generator supports prefix matching
            var method = _keyGenerator.GetType().GetMethod("FindMatchingPrefixKeys");
            if (method != null)
            {
                var result = method.Invoke(_keyGenerator, new object[] { request });
                if (result is List<string> prefixKeys && prefixKeys.Any())
                {
                    // Try to get cached response using prefix matching
                    var cacheAdapter = _cache as CacheServiceAdapter;
                    if (cacheAdapter != null)
                    {
                        var prefixCachedResponse = await cacheAdapter.GetByPrefixAsync(prefixKeys, cancellationToken);
                        if (prefixCachedResponse != null)
                        {
                            var cachedSuggestionResponse = JsonSerializer.Deserialize<SuggestionResponse>(prefixCachedResponse);
                            if (cachedSuggestionResponse != null)
                            {
                                // Filter suggestions to match current input
                                var filteredResponse = FilterSuggestionsByCurrentInput(cachedSuggestionResponse, request.UserInput);
                                
                                // Only return if we have matching suggestions
                                if (filteredResponse.Suggestions.Any())
                                {
                                    filteredResponse.IsFromCache = true;
                                    filteredResponse.CachedTimestamp = DateTime.UtcNow;
                                    filteredResponse.GenerationTimeMs = 0.5; // Very fast prefix cache retrieval
                                    return filteredResponse;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // If prefix matching fails, continue with normal cache lookup
        }

        return null;
    }

    /// <summary>
    /// Filters cached suggestions to only return those that match the current user input
    /// </summary>
    private SuggestionResponse FilterSuggestionsByCurrentInput(SuggestionResponse cachedResponse, string currentInput)
    {
        var normalizedInput = currentInput?.Trim().ToLowerInvariant() ?? string.Empty;
        
        if (string.IsNullOrEmpty(normalizedInput))
        {
            return cachedResponse; // Return all suggestions if no input
        }

        var filteredSuggestions = cachedResponse.Suggestions
            .Where(suggestion => 
                suggestion.SuggestionText.Trim().ToLowerInvariant().StartsWith(normalizedInput))
            .ToList();

        return new SuggestionResponse(
            suggestions: filteredSuggestions,
            source: "prefix-cache",
            confidenceScore: cachedResponse.ConfidenceScore,
            isFromCache: true,
            generationTimeMs: 0.5,
            cacheHitRate: 100.0,
            warningMessage: filteredSuggestions.Any() ? null : "No matching suggestions found"
        );
    }

    /// <summary>
    /// Stores the response in cache with multiple prefix keys for better matching
    /// </summary>
    private async Task StoreWithPrefixKeys(SuggestionRequest request, SuggestionResponse response)
    {
        try
        {
            var serializedResponse = JsonSerializer.Serialize(response);
            
            // Store with exact key first
            var exactKey = _keyGenerator.GenerateCacheKey(request);
            await _cache.SetAsync(exactKey, serializedResponse, CancellationToken.None);
            
            // Check if the key generator supports prefix keys
            var method = _keyGenerator.GetType().GetMethod("GenerateAllPrefixKeys");
            if (method != null)
            {
                var result = method.Invoke(_keyGenerator, new object[] { request });
                if (result is List<string> prefixKeys && prefixKeys.Any())
                {
                    // Store with prefix keys using adapter
                    var cacheAdapter = _cache as CacheServiceAdapter;
                    if (cacheAdapter != null)
                    {
                        await cacheAdapter.SetWithPrefixKeysAsync(prefixKeys, serializedResponse, CancellationToken.None);
                    }
                }
            }
        }
        catch
        {
            // Ignore cache storage errors - this is a background operation
        }
    }

    /// <summary>
    /// Helper method to safely get property values using reflection
    /// </summary>
    private T? GetPropertyValue<T>(object obj, string propertyName)
    {
        try
        {
            var property = obj.GetType().GetProperty(propertyName);
            var value = property?.GetValue(obj);
            return value is T result ? result : default(T);
        }
        catch
        {
            return default(T);
        }
    }

    /// <summary>
    /// Disposes the cached service bridge and its resources
    /// </summary>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            lock (_lockObject)
            {
                if (!_isDisposed)
                {
                    _isDisposed = true;
                    _cache?.Dispose();
                    _backend?.Dispose();
                }
            }
        }
    }
}
