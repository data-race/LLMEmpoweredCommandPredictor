using System;
using System.Threading;
using System.Threading.Tasks;
using LLMEmpoweredCommandPredictor.Protocol.Abstractions;
using LLMEmpoweredCommandPredictor.Protocol.Models;

namespace LLMEmpoweredCommandPredictor.Protocol.Adapters;

/// <summary>
/// Adapter that wraps PredictorCache implementations to work with Protocol abstractions.
/// This allows Protocol to use concrete cache implementations without direct dependencies.
/// </summary>
public class CacheServiceAdapter : ICacheService
{
    private readonly object _cacheService;
    private readonly object _keyGenerator;
    private volatile bool _disposed = false;

    /// <summary>
    /// Creates a new CacheServiceAdapter
    /// </summary>
    /// <param name="cacheService">The concrete cache service (e.g., InMemoryCache from PredictorCache)</param>
    /// <param name="keyGenerator">The concrete key generator (e.g., CacheKeyGenerator from PredictorCache)</param>
    public CacheServiceAdapter(object cacheService, object keyGenerator)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (_disposed) return null;

        try
        {
            // Use reflection to call GetAsync on the concrete cache service
            var method = _cacheService.GetType().GetMethod("GetAsync");
            if (method != null)
            {
                var result = method.Invoke(_cacheService, new object[] { cacheKey, cancellationToken });
                if (result is Task<string?> task)
                {
                    return await task;
                }
                else if (result is Task<string> stringTask)
                {
                    return await stringTask;
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(string cacheKey, string response, CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        try
        {
            // Use reflection to call SetAsync on the concrete cache service
            var method = _cacheService.GetType().GetMethod("SetAsync");
            if (method != null)
            {
                var task = method.Invoke(_cacheService, new object[] { cacheKey, response, cancellationToken }) as Task;
                if (task != null)
                    await task;
            }
        }
        catch
        {
            // Ignore cache errors
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        try
        {
            var method = _cacheService.GetType().GetMethod("RemoveAsync");
            if (method != null)
            {
                var task = method.Invoke(_cacheService, new object[] { cacheKey, cancellationToken }) as Task;
                if (task != null)
                    await task;
            }
        }
        catch
        {
            // Ignore cache errors
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        try
        {
            var method = _cacheService.GetType().GetMethod("ClearAsync");
            if (method != null)
            {
                var task = method.Invoke(_cacheService, new object[] { cancellationToken }) as Task;
                if (task != null)
                    await task;
            }
        }
        catch
        {
            // Ignore cache errors
        }
    }

    /// <inheritdoc />
    public ICacheStatistics GetStatistics()
    {
        if (_disposed) 
            return new CacheStatisticsAdapter();

        try
        {
            var method = _cacheService.GetType().GetMethod("GetStatistics");
            if (method != null)
            {
                var stats = method.Invoke(_cacheService, null);
                if (stats != null)
                    return new CacheStatisticsAdapter(stats);
            }
        }
        catch
        {
            // Return empty stats on error
        }

        return new CacheStatisticsAdapter();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            
            // Dispose the underlying cache service if it's disposable
            if (_cacheService is IDisposable disposableCache)
            {
                disposableCache.Dispose();
            }
        }
    }
}

/// <summary>
/// Adapter for cache key generation
/// </summary>
public class CacheKeyGeneratorAdapter : ICacheKeyGenerator
{
    private readonly object _keyGenerator;

    /// <summary>
    /// Creates a new CacheKeyGeneratorAdapter
    /// </summary>
    /// <param name="keyGenerator">The concrete key generator</param>
    public CacheKeyGeneratorAdapter(object keyGenerator)
    {
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
    }

    /// <inheritdoc />
    public string GenerateCacheKey(object request)
    {
        try
        {
            var method = _keyGenerator.GetType().GetMethod("GenerateCacheKey");
            if (method != null)
            {
                var result = method.Invoke(_keyGenerator, new object[] { request });
                return result?.ToString() ?? string.Empty;
            }
        }
        catch
        {
            // Return fallback key on error
        }

        return $"fallback_{request?.GetHashCode() ?? 0}";
    }
}

/// <summary>
/// Adapter for cache statistics
/// </summary>
public class CacheStatisticsAdapter : ICacheStatistics
{
    private readonly object? _stats;

    /// <summary>
    /// Creates empty statistics
    /// </summary>
    public CacheStatisticsAdapter()
    {
        _stats = null;
    }

    /// <summary>
    /// Creates statistics from concrete implementation
    /// </summary>
    /// <param name="stats">Concrete statistics object</param>
    public CacheStatisticsAdapter(object stats)
    {
        _stats = stats;
    }

    /// <inheritdoc />
    public int TotalRequests => GetPropertyValue<int>("TotalRequests");

    /// <inheritdoc />
    public int CacheHits => GetPropertyValue<int>("CacheHits");

    /// <inheritdoc />
    public int CacheMisses => GetPropertyValue<int>("CacheMisses");

    /// <inheritdoc />
    public double HitRate => GetPropertyValue<double>("HitRate");

    /// <inheritdoc />
    public int TotalEntries => GetPropertyValue<int>("TotalEntries");

    /// <inheritdoc />
    public long MemoryUsageBytes => GetPropertyValue<long>("MemoryUsageBytes");

    /// <inheritdoc />
    public DateTime LastAccess => GetPropertyValue<DateTime>("LastAccess");

    /// <inheritdoc />
    public TimeSpan Uptime => GetPropertyValue<TimeSpan>("Uptime");

    /// <summary>
    /// Helper method to safely get property values using reflection
    /// </summary>
    private T GetPropertyValue<T>(string propertyName)
    {
        try
        {
            if (_stats != null)
            {
                var property = _stats.GetType().GetProperty(propertyName);
                var value = property?.GetValue(_stats);
                if (value is T result)
                    return result;
            }
        }
        catch
        {
            // Return default on error
        }

        return default(T)!;
    }
}
