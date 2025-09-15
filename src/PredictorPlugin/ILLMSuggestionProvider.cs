using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation.Subsystem.Prediction;

namespace LLMEmpoweredCommandPredictor;

public interface ILLMSuggestionProvider
{
    /// <summary>
    /// Gets predictive suggestions based on the provided context.
    /// </summary>
    /// <param name="context">The context information used for generating suggestions.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A list of predictive suggestions.</returns>
    List<PredictiveSuggestion> GetSuggestions(LLMSuggestionContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a user command to the backend service for learning purposes.
    /// </summary>
    /// <param name="commandLine">The command that was executed</param>
    /// <param name="success">Whether the command executed successfully</param>
    /// <returns>Task that completes when command is saved</returns>
    Task SaveCommandAsync(string commandLine, bool success);
}