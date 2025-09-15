using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;
using LLMEmpoweredCommandPredictor.Protocol.Contracts;

namespace LLMEmpoweredCommandPredictor.Protocol.Server;

/// <summary>
/// Simple server implementation for the Suggestion Service.
/// Handles incoming RPC calls from clients with minimal overhead.
/// </summary>
public class SuggestionServiceServer : IDisposable
{
    private readonly ISuggestionService _service;
    private readonly string _pipeName;
    private NamedPipeServerStream? _pipeServer;
    private JsonRpc? _rpc;
    private bool _disposed;
    private bool _isRunning;

    /// <summary>
    /// Gets whether the server is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Initializes a new instance of the SuggestionServiceServer.
    /// </summary>
    /// <param name="service">The suggestion service implementation</param>
    /// <param name="pipeName">Named pipe name for communication</param>
    public SuggestionServiceServer(
        ISuggestionService service,
        string pipeName = "LLMEmpoweredCommandPredictor.SuggestionService")
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
    }

    /// <summary>
    /// Starts the server and begins accepting client connections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when the server starts</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SuggestionServiceServer));
        }

        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        
        // Start listening in the background instead of blocking
        _ = Task.Run(() => StartListeningAsync(cancellationToken));
        
        // Return immediately after starting background task
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the server and closes all client connections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when the server stops</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;

        // Close the RPC connection
        _rpc?.Dispose();
        _rpc = null;

        // Close the pipe server
        _pipeServer?.Close();
#pragma warning disable VSTHRD103 // Dispose synchronously blocks
        _pipeServer?.Dispose();
#pragma warning restore VSTHRD103
        _pipeServer = null;

        await Task.CompletedTask;
    }

    /// <summary>
    /// Starts listening for client connections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Create pipe server
                _pipeServer = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    1, // Single connection
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                // Wait for client connection
                await _pipeServer.WaitForConnectionAsync(cancellationToken);

                // Create JsonRpc instance
                _rpc = new JsonRpc(_pipeServer);
                _rpc.AddLocalRpcTarget(_service);
                _rpc.StartListening();

                // Wait for the connection to close
                await _rpc.Completion;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Simple error handling - just continue listening
                await Task.Delay(1000, cancellationToken);
            }
            finally
            {
                // Clean up
#pragma warning disable VSTHRD103 // Dispose synchronously blocks
                _rpc?.Dispose();
#pragma warning restore VSTHRD103
                _rpc = null;
#pragma warning disable VSTHRD103 // Dispose synchronously blocks
                _pipeServer?.Dispose();
#pragma warning restore VSTHRD103
                _pipeServer = null;
            }
        }
    }

    /// <summary>
    /// Disposes of the server and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop the server if it's running
        if (_isRunning)
        {
            _ = StopAsync().ConfigureAwait(false);
        }

#pragma warning disable VSTHRD103 // Dispose synchronously blocks
        _rpc?.Dispose();
        _pipeServer?.Dispose();
#pragma warning restore VSTHRD103
    }
}
