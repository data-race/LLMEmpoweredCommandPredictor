using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LLMEmpoweredCommandPredictor.Protocol.Contracts;
using LLMEmpoweredCommandPredictor.Protocol.Models;
using LLMEmpoweredCommandPredictor.Protocol.Adapters;
using LLMEmpoweredCommandPredictor.Protocol.Extensions;

namespace LLMEmpoweredCommandPredictor.Protocol.Integration;

/// <summary>
/// Bridge implementation that connects Protocol layer with PredictorService.
/// This class can be used by PredictorService to implement ISuggestionService
/// without requiring changes to existing Service code.
/// </summary>
public class ServiceBridge : ISuggestionService
{
    private readonly IServiceBackend _backend;
    private readonly object _lockObject = new object();
    private volatile bool _isDisposed = false;

    /// <summary>
    /// Creates a new ServiceBridge with the provided backend implementation
    /// </summary>
    /// <param name="backend">The backend service implementation</param>
    public ServiceBridge(IServiceBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    /// <inheritdoc />
    public async Task<SuggestionResponse> GetSuggestionsAsync(
        SuggestionRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ServiceBridge));

        if (request == null)
        {
            return new SuggestionResponse(
                suggestions: new List<PredictiveSuggestionDto>(),
                source: "error",
                warningMessage: "Invalid request"
            );
        }

        try
        {
            // 1. Transform Protocol request to Service context
            var serviceContext = ContextTransformer.ToServiceContext(request);
            
            // 2. Call backend service
            var serviceResponse = await _backend.ProcessSuggestionAsync(
                serviceContext, 
                request.MaxSuggestions, 
                cancellationToken);
            
            // 3. Transform Service response back to Protocol format
            return ContextTransformer.FromServiceResponse(
                serviceResponse, 
                request.MaxSuggestions, 
                isFromCache: false);
        }
        catch (OperationCanceledException)
        {
            return new SuggestionResponse(
                suggestions: new List<PredictiveSuggestionDto>(),
                source: "cancelled",
                warningMessage: "Request was cancelled"
            );
        }
        catch (Exception ex)
        {
            return new SuggestionResponse(
                suggestions: new List<PredictiveSuggestionDto>(),
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
            
            // Transform backend status to Protocol ServiceStatus
            return TransformBackendStatus(backendStatus);
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
            await _backend.ClearCacheAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Log error but don't throw
        }
    }

    /// <summary>
    /// Transforms backend status to Protocol ServiceStatus format
    /// </summary>
    private ServiceStatus TransformBackendStatus(object backendStatus)
    {
        // Use reflection to extract status information
        try
        {
            var statusType = backendStatus.GetType();
            
            var isRunning = GetPropertyValue<bool?>(backendStatus, "IsRunning") ?? true;
            var uptime = GetPropertyValue<TimeSpan?>(backendStatus, "Uptime") ?? TimeSpan.Zero;
            var errorMessage = GetPropertyValue<string>(backendStatus, "ErrorMessage");
            
            return new ServiceStatus(
                isRunning: isRunning,
                uptime: uptime,
                errorMessage: errorMessage
            );
        }
        catch
        {
            return new ServiceStatus(isRunning: true);
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
    /// Disposes the service bridge
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
                    _backend?.Dispose();
                }
            }
        }
    }
}

/// <summary>
/// Interface that PredictorService can implement to work with ServiceBridge.
/// This allows the Protocol layer to communicate with Service without tight coupling.
/// </summary>
public interface IServiceBackend : IDisposable
{
    /// <summary>
    /// Processes a suggestion request using existing Service logic
    /// </summary>
    /// <param name="context">Service context object</param>
    /// <param name="maxSuggestions">Maximum number of suggestions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Service response object</returns>
    Task<object> ProcessSuggestionAsync(object context, int maxSuggestions, CancellationToken cancellationToken);
    
    /// <summary>
    /// Checks if the backend service is healthy
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets backend service status
    /// </summary>
    Task<object> GetStatusAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Triggers cache prewarming for the given context
    /// </summary>
    Task PrewarmCacheAsync(object context, CancellationToken cancellationToken);
    
    /// <summary>
    /// Clears the backend cache
    /// </summary>
    Task ClearCacheAsync(CancellationToken cancellationToken);
}
