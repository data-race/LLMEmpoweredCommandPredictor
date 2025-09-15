using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LLMEmpoweredCommandPredictor.Protocol.Client;
using LLMEmpoweredCommandPredictor.Protocol.Models;
using LLMEmpoweredCommandPredictor.Protocol.Adapters;
using LLMEmpoweredCommandPredictor.Protocol.Extensions;

namespace LLMEmpoweredCommandPredictor.Protocol.Integration;

/// <summary>
/// Helper class that makes it easy for Plugin to use Protocol IPC communication.
/// This class bridges the gap between Plugin's existing API and Protocol's IPC layer.
/// </summary>
public class PluginHelper : IDisposable
{
    private readonly SuggestionServiceClient _ipcClient;
    private readonly ConnectionSettings _connectionSettings;
    private readonly object _lockObject = new object();
    private volatile bool _isDisposed = false;

    /// <summary>
    /// Creates a new PluginHelper with default connection settings
    /// </summary>
    public PluginHelper() : this(new ConnectionSettings())
    {
    }

    /// <summary>
    /// Creates a new PluginHelper with custom connection settings
    /// </summary>
    /// <param name="connectionSettings">IPC connection settings</param>
    public PluginHelper(ConnectionSettings connectionSettings)
    {
        _connectionSettings = connectionSettings ?? throw new ArgumentNullException(nameof(connectionSettings));
        _ipcClient = new SuggestionServiceClient(_connectionSettings);
    }

    /// <summary>
    /// Gets suggestions from the backend service via IPC.
    /// This method can be called from Plugin's GetSuggestions method.
    /// </summary>
    /// <param name="pluginContext">Plugin's context object (LLMSuggestionContext)</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of PowerShell PredictiveSuggestion objects</returns>
    public async Task<IList<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>> GetSuggestionsAsync(
        object pluginContext,
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            return new List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>();

        try
        {
            // 1. Transform Plugin context to Protocol request
            var protocolRequest = ContextTransformer.FromPluginContext(
                pluginContext, 
                maxSuggestions);

            // 2. Make IPC call to backend service
            var response = await _ipcClient.GetSuggestionsAsync(protocolRequest, cancellationToken);

            // 3. Convert DTO to PowerShell suggestions
            return response.Suggestions.ToPowerShell();
        }
        catch (OperationCanceledException)
        {
            return new List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>();
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            System.Console.WriteLine($"[PluginHelper] Error in GetSuggestionsAsync: {ex.Message}");
            System.Console.WriteLine($"[PluginHelper] Exception type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                System.Console.WriteLine($"[PluginHelper] Inner exception: {ex.InnerException.Message}");
            }
            
            // Return fallback suggestions on error
            return CreateFallbackSuggestions(pluginContext);
        }
    }

    /// <summary>
    /// Gets suggestions synchronously (for compatibility with existing Plugin API).
    /// This method blocks the calling thread, so use with caution.
    /// </summary>
    /// <param name="pluginContext">Plugin's context object</param>
    /// <param name="maxSuggestions">Maximum number of suggestions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of suggestions</returns>
    public IList<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion> GetSuggestions(
        object pluginContext,
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            System.Console.WriteLine($"[PluginHelper] Starting GetSuggestions with timeout: {_connectionSettings.TimeoutMs}ms");
            
            // Use a short timeout to respect the 20ms constraint
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_connectionSettings.TimeoutMs));

            var task = GetSuggestionsAsync(pluginContext, maxSuggestions, timeoutCts.Token);
            
            System.Console.WriteLine($"[PluginHelper] Waiting for task completion...");
            
            #pragma warning disable VSTHRD002 // Synchronously waiting on tasks or awaiters may cause deadlocks
            // Wait with timeout
            if (task.Wait(_connectionSettings.TimeoutMs, cancellationToken))
            {
                System.Console.WriteLine($"[PluginHelper] Task completed successfully, returning {task.Result.Count} suggestions");
                return task.Result;
            }
            #pragma warning restore VSTHRD002
            else
            {
                System.Console.WriteLine($"[PluginHelper] Task timed out after {_connectionSettings.TimeoutMs}ms, returning fallback");
                // Timeout - return fallback
                return CreateFallbackSuggestions(pluginContext);
            }
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            System.Console.WriteLine($"[PluginHelper] Error in GetSuggestions (sync): {ex.Message}");
            System.Console.WriteLine($"[PluginHelper] Exception type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                System.Console.WriteLine($"[PluginHelper] Inner exception: {ex.InnerException.Message}");
            }
            
            return CreateFallbackSuggestions(pluginContext);
        }
    }

    /// <summary>
    /// Triggers background cache refresh for better future performance.
    /// This is a fire-and-forget operation that doesn't block.
    /// Note: Currently simplified since client doesn't have cache refresh method.
    /// </summary>
    /// <param name="pluginContext">Plugin's context object</param>
    public void TriggerBackgroundRefresh(object pluginContext)
    {
        if (_isDisposed)
            return;

        try
        {
            // Fire and forget - trigger another suggestion request to warm cache
            _ = Task.Run(async () =>
            {
                try
                {
                    var protocolRequest = ContextTransformer.FromPluginContext(pluginContext);
                    // Make a background request to potentially warm the cache
                    await _ipcClient.GetSuggestionsAsync(protocolRequest, CancellationToken.None);
                }
                catch
                {
                    // Ignore errors in background operations
                }
            });
        }
        catch
        {
            // Ignore errors
        }
    }

    /// <summary>
    /// Checks if the backend service is available
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds (default: 100ms)</param>
    /// <returns>True if service is available</returns>
    public async Task<bool> IsServiceAvailableAsync(int timeoutMs = 100)
    {
        if (_isDisposed)
            return false;

        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            // Try a simple suggestion request to test connectivity
            var testRequest = new SuggestionRequest("test", maxSuggestions: 1);
            var response = await _ipcClient.GetSuggestionsAsync(testRequest, cts.Token);
            return response != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets service status information for debugging
    /// </summary>
    /// <returns>Service status or null if unavailable</returns>
    public async Task<ServiceStatus?> GetServiceStatusAsync()
    {
        if (_isDisposed)
            return null;

        try
        {
            // Since client doesn't have GetStatusAsync, return basic status based on connectivity
            var isAvailable = await IsServiceAvailableAsync(1000);
            return new ServiceStatus(isRunning: isAvailable);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates fallback suggestions when IPC fails.
    /// This ensures Plugin always returns something useful.
    /// </summary>
    private IList<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion> CreateFallbackSuggestions(object pluginContext)
    {
        try
        {
            // Extract user input from plugin context
            var userInput = GetPropertyValue<string>(pluginContext, "UserInput") ?? "";
            
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return new List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>();
            }

            // Create basic fallback suggestions
            var suggestions = new List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>();

            // Add some common PowerShell completions based on input
            if (userInput.StartsWith("Get-", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add(new System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion(
                    $"{userInput} | Format-Table",
                    "Format output as table"));
                suggestions.Add(new System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion(
                    $"{userInput} | Select-Object -First 10",
                    "Get first 10 results"));
            }
            else if (userInput.Contains("Process", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add(new System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion(
                    "Get-Process | Sort-Object CPU -Descending",
                    "Get processes sorted by CPU usage"));
            }
            else
            {
                // Generic fallback
                suggestions.Add(new System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion(
                    $"{userInput} -?",
                    "Get help for this command"));
            }

            return suggestions;
        }
        catch
        {
            return new List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>();
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
    /// Disposes the PluginHelper and its resources
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
                    _ipcClient?.Dispose();
                }
            }
        }
    }
}
