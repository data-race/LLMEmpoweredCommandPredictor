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
        _logger.LogInformation("PowerShell Plugin Provider: GetSuggestions called for input: '{UserInput}'", context.UserInput);
        
        // SIMPLE TEST: Always return test suggestions for ANY input to debug display
        _logger.LogInformation("PowerShell Plugin Provider: ALWAYS returning test suggestions for debugging");
        return new List<PredictiveSuggestion>
        {
            new("TEST: git status"),
            new("TEST: git add ."),
            new("TEST: git commit"),
            new("TEST: git push"),
            new("TEST: git pull")
        };
    }
}
