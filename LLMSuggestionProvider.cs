using System.Collections.Generic;
using System.Threading;
using System.Management.Automation.Subsystem.Prediction;
using System;

namespace LLMEmpoweredCommandPredictor;

/// <summary>
/// Default implementation of the ILLMSuggestionProvider interface.
/// </summary>
public class LLMSuggestionProvider : ILLMSuggestionProvider
{
    public LLMSuggestionProvider()
    {
        // TODO: add initialization logic
    }

    /// <summary>
    /// Gets predictive suggestions based on the provided context.
    /// </summary>
    /// <param name="context">The context information used for generating suggestions.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A list of predictive suggestions.</returns>
    public List<PredictiveSuggestion> GetSuggestions(LLMSuggestionContext context, CancellationToken cancellationToken)
    {
        // Implementation will be added later
        return new List<PredictiveSuggestion>{
                new(string.Concat(context.UserInput, " HELLO WORLD 1"), "Test tool tip"),
                new(string.Concat(context.UserInput, " HELLO WORLD 2")),
                new(string.Concat(context.UserInput, " HELLO WORLD 3"))
            };
    }
}
