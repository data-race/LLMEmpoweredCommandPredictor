using System.Threading;
using System.Threading.Tasks;

namespace LLMEmpoweredCommandPredictor.PredictorService.Context;

/// <summary>
/// Interface for collecting context information needed for command prediction.
/// Different implementations can collect context from various sources.
/// </summary>
public interface IContextProvider
{
    /// <summary>
    /// Gets the name of this context provider
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the priority of this context provider (higher numbers = higher priority)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Indicates whether this provider is currently available and functional
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Collects context information asynchronously.
    /// This method should be fast and non-blocking.
    /// </summary>
    /// <param name="userInput">The current user input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The collected context information</returns>
    Task<PredictorContext> CollectContextAsync(string userInput, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the context with new information (e.g., after command execution).
    /// This is called to provide feedback and improve future context collection.
    /// </summary>
    /// <param name="context">The context to update</param>
    /// <param name="executedCommand">The command that was executed</param>
    /// <param name="wasSuccessful">Whether the command executed successfully</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task UpdateContextAsync(
        PredictorContext context, 
        string executedCommand, 
        bool wasSuccessful, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the context provider. Called once when the service starts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up resources when the service shuts down.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task DisposeAsync(CancellationToken cancellationToken = default);
}
