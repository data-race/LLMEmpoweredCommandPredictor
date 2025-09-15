using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation.Subsystem.Prediction;
using LLMEmpoweredCommandPredictor.Protocol.Models;

namespace LLMEmpoweredCommandPredictor.Protocol.Contracts;

/// <summary>
/// Core contract for communication between the Command Predictor (client) and Background Service (server).
/// This interface defines the RPC methods that can be called remotely using StreamJsonRpc.
/// 
/// Design Principles:
/// 1. Contract-First: This is the single source of truth for all client-server communication
/// 2. Non-blocking: All methods are async to prevent UI thread blocking
/// 3. Fast response: Methods must return quickly (within 20ms constraint)
/// 4. Robust: Handling of connection issues and timeouts
/// </summary>
public interface ISuggestionService
{
    /// <summary>
    /// Gets command suggestions based on the current user input and context.
    /// This is the primary method called by the PowerShell predictor plugin.
    /// 
    /// Performance Requirements:
    /// - Must return within 20ms to avoid blocking PowerShell UI thread
    /// - Returns cached suggestions immediately if available
    /// - Returns empty response if no suggestions are cached
    /// - Never blocks waiting for LLM generation
    /// </summary>
    /// <param name="request">The suggestion request containing user input and context</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Suggestion response containing suggestions and metadata</returns>
    Task<SuggestionResponse> GetSuggestionsAsync(
        SuggestionRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pings the service to check if it's alive and responsive.
    /// Used for health checking and connection validation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>True if service is responsive, false otherwise</returns>
    Task<bool> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets service status and statistics.
    /// Used for monitoring and debugging purposes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Service status information</returns>
    Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers background cache refresh for the given context.
    /// This method is called asynchronously to pre-populate the cache.
    /// 
    /// Performance Requirements:
    /// - Returns immediately (does not wait for LLM generation)
    /// - Triggers background task for cache population
    /// - Non-blocking operation
    /// </summary>
    /// <param name="request">The suggestion request to pre-populate cache for</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Task that completes when background refresh is initiated</returns>
    Task TriggerCacheRefreshAsync(
        SuggestionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the suggestion cache.
    /// Used for maintenance and debugging purposes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Task that completes when cache is cleared</returns>
    Task ClearCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a user command to the cache for future suggestions.
    /// This method is called when a user executes a command to learn from their patterns.
    /// </summary>
    /// <param name="commandLine">The command that was executed</param>
    /// <param name="success">Whether the command executed successfully</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Task that completes when command is saved to cache</returns>
    Task SaveCommandAsync(string commandLine, bool success, CancellationToken cancellationToken = default);
}
