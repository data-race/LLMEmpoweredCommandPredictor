using System.Collections.Generic;
using System.Threading;
using System.Management.Automation.Subsystem.Prediction;
using System;
using System.Linq;
using LLMEmpoweredCommandPredictor.Protocol.Integration;

namespace LLMEmpoweredCommandPredictor;

/// <summary>
/// Default implementation of the ILLMSuggestionProvider interface.
/// </summary>
public class LLMSuggestionProvider : ILLMSuggestionProvider
{
    private readonly PluginHelper _pluginHelper;

    public LLMSuggestionProvider()
    {
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
            return _pluginHelper.GetSuggestions(context, 5, cancellationToken).ToList();
        }
        catch
        {
            return new List<PredictiveSuggestion>{
                new(string.Concat(context.UserInput, " (fallback)"))
            };
        }
    }
}
