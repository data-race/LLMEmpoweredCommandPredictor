using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation.Subsystem.Prediction;
using System;
using System.Linq;
using LLMEmpoweredCommandPredictor.Protocol.Integration;
using LLMEmpoweredCommandPredictor.PredictorCache;
using Microsoft.Extensions.Logging;

namespace LLMEmpoweredCommandPredictor;

/// <summary>
/// Default implementation of the ILLMSuggestionProvider interface.
/// </summary>
public class LLMSuggestionProvider : ILLMSuggestionProvider
{
    private readonly PluginHelper _pluginHelper;
    private readonly ILogger<LLMSuggestionProvider> _logger;

    public LLMSuggestionProvider()
    {
        // Create a console logger for the plugin
        _logger = ConsoleLoggerFactory.CreateDebugLogger<LLMSuggestionProvider>();
        _pluginHelper = new PluginHelper();
    }

    /// <summary>
    /// Gets predictive suggestions based on the provided context.
    /// </summary>
    /// <param name="context">The context information used for generating suggestions.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A list of predictive suggestions.</returns>
    public List<PredictiveSuggestion> GetSuggestions(LLMSuggestionContext context, CancellationToken cancellationToken)
    {
        try
        {
            // Get suggestions from the backend service via IPC
            var suggestions = _pluginHelper.GetSuggestions(context, 5, cancellationToken).ToList();
            
            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PowerShell Plugin Provider: Error getting suggestions from backend: {Error}", ex.Message);
            _logger.LogDebug("PowerShell Plugin Provider: Exception details: {StackTrace}", ex.StackTrace);
            
            // Return empty list if backend fails - no hardcoded fallbacks
            return new List<PredictiveSuggestion>();
        }
    }

    /// <summary>
    /// Saves a user command to the backend service for learning purposes.
    /// </summary>
    /// <param name="commandLine">The command that was executed</param>
    /// <param name="success">Whether the command executed successfully</param>
    /// <returns>Task that completes when command is saved</returns>
    public async Task SaveCommandAsync(string commandLine, bool success)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return;
        }

        try
        {
            // Call the backend service to save the command
            await _pluginHelper.SaveCommandAsync(commandLine, success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PowerShell Plugin Provider: Failed to save command '{CommandLine}': {Error}", 
                commandLine, ex.Message);
        }
    }
}
