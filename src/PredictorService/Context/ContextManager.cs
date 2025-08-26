using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LLMEmpoweredCommandPredictor.PredictorService.Context;

/// <summary>
/// Manages multiple context providers and coordinates context collection.
/// This class aggregates context from different sources and provides a unified interface.
/// </summary>
public class ContextManager : IDisposable
{
    private readonly ILogger<ContextManager> _logger;
    private readonly List<IContextProvider> _providers;
    private readonly object _providersLock = new();
    private bool _disposed = false;

    public ContextManager(ILogger<ContextManager> logger)
    {
        _logger = logger;
        _providers = new List<IContextProvider>();
    }

    /// <summary>
    /// Gets the list of registered context providers.
    /// </summary>
    public IReadOnlyList<IContextProvider> Providers
    {
        get
        {
            lock (_providersLock)
            {
                return _providers.ToList();
            }
        }
    }

    /// <summary>
    /// Registers a context provider.
    /// </summary>
    /// <param name="provider">The context provider to register</param>
    public void RegisterProvider(IContextProvider provider)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        lock (_providersLock)
        {
            if (!_providers.Contains(provider))
            {
                _providers.Add(provider);
                _providers.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // Sort by priority descending
                _logger.LogInformation("Registered context provider: {ProviderName} (Priority: {Priority})", 
                    provider.Name, provider.Priority);
            }
        }
    }

    /// <summary>
    /// Initializes all registered context providers.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing Context Manager with {Count} providers...", _providers.Count);

        var initializationTasks = new List<Task>();

        lock (_providersLock)
        {
            foreach (var provider in _providers)
            {
                initializationTasks.Add(InitializeProviderAsync(provider, cancellationToken));
            }
        }

        await Task.WhenAll(initializationTasks);

        var availableProviders = _providers.Count(p => p.IsAvailable);
        _logger.LogInformation("Context Manager initialized. Available providers: {Available}/{Total}", 
            availableProviders, _providers.Count);
    }

    /// <summary>
    /// Collects context from all available providers and merges the results.
    /// </summary>
    /// <param name="userInput">The current user input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Merged context from all providers</returns>
    public async Task<PredictorContext> CollectContextAsync(string userInput, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Collecting context for user input: '{UserInput}'", userInput);

        var contextTasks = new List<Task<PredictorContext>>();

        lock (_providersLock)
        {
            foreach (var provider in _providers.Where(p => p.IsAvailable))
            {
                contextTasks.Add(CollectFromProviderAsync(provider, userInput, cancellationToken));
            }
        }

        if (contextTasks.Count == 0)
        {
            _logger.LogWarning("No available context providers found");
            return CreateFallbackContext(userInput);
        }

        var contexts = await Task.WhenAll(contextTasks);
        var mergedContext = MergeContexts(contexts, userInput);

        _logger.LogDebug("Context collection completed: {Summary}", mergedContext.GetSummary());
        return mergedContext;
    }

    /// <summary>
    /// Updates context across all providers after command execution.
    /// </summary>
    /// <param name="context">The context to update</param>
    /// <param name="executedCommand">The command that was executed</param>
    /// <param name="wasSuccessful">Whether the command executed successfully</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task UpdateContextAsync(
        PredictorContext context, 
        string executedCommand, 
        bool wasSuccessful, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating context across providers for command: '{Command}'", executedCommand);

        var updateTasks = new List<Task>();

        lock (_providersLock)
        {
            foreach (var provider in _providers.Where(p => p.IsAvailable))
            {
                updateTasks.Add(UpdateProviderAsync(provider, context, executedCommand, wasSuccessful, cancellationToken));
            }
        }

        await Task.WhenAll(updateTasks);
        _logger.LogDebug("Context update completed across {Count} providers", updateTasks.Count);
    }

    /// <summary>
    /// Disposes all context providers.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogInformation("Disposing Context Manager...");

        var disposeTasks = new List<Task>();

        lock (_providersLock)
        {
            foreach (var provider in _providers)
            {
                disposeTasks.Add(DisposeProviderAsync(provider));
            }
        }

        try
        {
            Task.WaitAll(disposeTasks.ToArray(), TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing context providers");
        }

        lock (_providersLock)
        {
            _providers.Clear();
        }

        _disposed = true;
        _logger.LogInformation("Context Manager disposed");
    }

    #region Private Methods

    private async Task InitializeProviderAsync(IContextProvider provider, CancellationToken cancellationToken)
    {
        try
        {
            await provider.InitializeAsync(cancellationToken);
            _logger.LogDebug("Provider {ProviderName} initialized successfully", provider.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize provider {ProviderName}", provider.Name);
        }
    }

    private async Task<PredictorContext> CollectFromProviderAsync(
        IContextProvider provider, 
        string userInput, 
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.CollectContextAsync(userInput, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting context from provider {ProviderName}", provider.Name);
            return CreateFallbackContext(userInput);
        }
    }

    private async Task UpdateProviderAsync(
        IContextProvider provider, 
        PredictorContext context, 
        string executedCommand, 
        bool wasSuccessful, 
        CancellationToken cancellationToken)
    {
        try
        {
            await provider.UpdateContextAsync(context, executedCommand, wasSuccessful, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating context in provider {ProviderName}", provider.Name);
        }
    }

    private async Task DisposeProviderAsync(IContextProvider provider)
    {
        try
        {
            await provider.DisposeAsync(CancellationToken.None);
            _logger.LogDebug("Provider {ProviderName} disposed successfully", provider.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing provider {ProviderName}", provider.Name);
        }
    }

    private PredictorContext MergeContexts(PredictorContext[] contexts, string userInput)
    {
        if (contexts.Length == 0)
        {
            return CreateFallbackContext(userInput);
        }

        // Take the first context as base and merge others into it
        var primaryContext = contexts[0];
        
        // For now, we just use the primary context
        // In the future, we could merge data from multiple providers
        return primaryContext;
    }

    private PredictorContext CreateFallbackContext(string userInput)
    {
        return new PredictorContext
        {
            UserInput = userInput ?? string.Empty,
            WorkingDirectory = Environment.CurrentDirectory,
            CommandHistory = Array.Empty<CommandHistoryEntry>(),
            Timestamp = DateTime.UtcNow,
            SessionId = "fallback",
            PowerShellVersion = "unknown",
            OperatingSystem = Environment.OSVersion.ToString()
        };
    }

    #endregion
}
