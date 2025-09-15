using System.Collections.Generic;
using System.Threading;
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
        
        _logger.LogInformation("PowerShell Plugin: LLMSuggestionProvider initialized");
    }

    /// <summary>
    /// Gets predictive suggestions based on the provided context.
    /// </summary>
    /// <param name="context">The context information used for generating suggestions.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A list of predictive suggestions.</returns>
    public List<PredictiveSuggestion> GetSuggestions(LLMSuggestionContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("PowerShell Plugin: GetSuggestions called for input: {UserInput}", context.UserInput);
        
        try
        {
            System.Console.WriteLine($"[LLMSuggestionProvider] Calling PluginHelper.GetSuggestions for: {context.UserInput}");
            var suggestions = _pluginHelper.GetSuggestions(context, 5, cancellationToken).ToList();
            System.Console.WriteLine($"[LLMSuggestionProvider] Received {suggestions.Count} suggestions from backend");
            _logger.LogDebug("PowerShell Plugin: Received {Count} suggestions from backend", suggestions.Count);
            
            foreach (var suggestion in suggestions.Take(3)) // Log first 3 suggestions
            {
                _logger.LogDebug("PowerShell Plugin: Suggestion: {SuggestionText}", suggestion.SuggestionText);
            }
            
            return suggestions;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[LLMSuggestionProvider] Exception: {ex.Message}");
            System.Console.WriteLine($"[LLMSuggestionProvider] Exception type: {ex.GetType().Name}");
            _logger.LogWarning("PowerShell Plugin: Error getting suggestions: {Error}", ex.Message);
            var fallback = new List<PredictiveSuggestion>{
                new(string.Concat(context.UserInput, " (fallback)"))
            };
            System.Console.WriteLine($"[LLMSuggestionProvider] Returning {fallback.Count} fallback suggestions");
            _logger.LogDebug("PowerShell Plugin: Returning fallback suggestion");
            return fallback;
        }
    }
}
