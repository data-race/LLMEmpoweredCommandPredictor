using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Management.Automation.Subsystem.Prediction;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;
using LLMEmpoweredCommandPredictor.Protocol.Contracts;
using LLMEmpoweredCommandPredictor.Protocol.Models;

namespace LLMEmpoweredCommandPredictor.Protocol.Client;

/// <summary>
/// Simple client implementation for communicating with the Suggestion Service.
/// Provides basic connection management with timeout handling.
/// </summary>
public class SuggestionServiceClient : IDisposable
{
    private readonly ConnectionSettings _settings;
    private JsonRpc? _rpc;
    private NamedPipeClientStream? _pipe;
    private ISuggestionService? _service;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the SuggestionServiceClient.
    /// </summary>
    /// <param name="settings">Connection settings</param>
    public SuggestionServiceClient(ConnectionSettings? settings = null)
    {
        _settings = settings ?? new ConnectionSettings();
    }

    /// <summary>
    /// Gets command suggestions from the service.
    /// Returns within 20ms to avoid blocking PowerShell UI thread.
    /// </summary>
    /// <param name="request">The suggestion request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Suggestion response, empty if service unavailable</returns>
    public async Task<SuggestionResponse> GetSuggestionsAsync(
        SuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get or establish connection
            var service = await GetServiceAsync(cancellationToken);
            if (service == null)
            {
                return CreateEmptyResponse("Service unavailable");
            }

            // Create timeout to ensure we don't exceed 20ms
            using var timeoutCts = new CancellationTokenSource(_settings.TimeoutMs);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCts.Token, cancellationToken);

            var response = await service.GetSuggestionsAsync(request, combinedCts.Token);
            return response ?? CreateEmptyResponse("No response from service");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CreateEmptyResponse("Request cancelled");
        }
        catch (Exception ex)
        {
            // Log error if debug logging is enabled
            if (_settings.EnableDebugLogging)
            {
#pragma warning disable VSTHRD103 // Call async methods when in an async method
                Console.Error.WriteLine($"[SuggestionServiceClient] Error: {ex.Message}");
#pragma warning restore VSTHRD103
            }

            // Clean up connection on error
            CleanupConnection();
            return CreateEmptyResponse($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets or establishes a connection to the service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Service interface or null if connection failed</returns>
    private async Task<ISuggestionService?> GetServiceAsync(CancellationToken cancellationToken)
    {
        // Return existing service if available
        if (_service != null)
        {
            return _service;
        }

        try
        {
            // Create named pipe client
            var pipe = new NamedPipeClientStream(
                ".", "LLMEmpoweredCommandPredictor.SuggestionService", 
                PipeDirection.InOut, PipeOptions.Asynchronous);

            // Connect with timeout
            using var connectCts = new CancellationTokenSource(_settings.ConnectionTimeoutMs);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                connectCts.Token, cancellationToken);

            await pipe.ConnectAsync(combinedCts.Token);

            // Create JsonRpc and service
            var rpc = new JsonRpc(pipe);
            rpc.StartListening();
            var service = rpc.Attach<ISuggestionService>();

            // Store connection
            _pipe = pipe;
            _rpc = rpc;
            _service = service;

            return service;
        }
        catch (Exception)
        {
            // Connection failed - return null
            return null;
        }
    }

    /// <summary>
    /// Creates an empty response with the specified warning message.
    /// </summary>
    /// <param name="warningMessage">Warning message</param>
    /// <returns>Empty suggestion response</returns>
    private static SuggestionResponse CreateEmptyResponse(string warningMessage)
    {
        return new SuggestionResponse
        {
            Suggestions = new List<PredictiveSuggestion>(),
            Source = "fallback",
            ConfidenceScore = 0.0,
            WarningMessage = warningMessage
        };
    }

    /// <summary>
    /// Cleans up the current connection.
    /// </summary>
    private void CleanupConnection()
    {
        _service = null;
        
        if (_rpc != null)
        {
#pragma warning disable VSTHRD103 // Dispose synchronously blocks
            _rpc.Dispose();
#pragma warning restore VSTHRD103
            _rpc = null;
        }

        if (_pipe != null)
        {
#pragma warning disable VSTHRD103 // Dispose synchronously blocks
            _pipe.Dispose();
#pragma warning restore VSTHRD103
            _pipe = null;
        }
    }

    /// <summary>
    /// Disposes of the client and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CleanupConnection();
    }
}
