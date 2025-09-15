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
        
        // Start the first pipe server immediately to ensure it's available
        try
        {
            _pipeServer = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                1, // Single connection
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            Console.WriteLine($"[SuggestionServiceServer] Named pipe '{_pipeName}' created and waiting for connections...");
            
            // Start listening in the background
            _ = Task.Run(() => StartListeningAsync(cancellationToken));
            
            // Give the pipe a moment to initialize
            await Task.Delay(100, cancellationToken);
            
            Console.WriteLine($"[SuggestionServiceServer] Server started successfully on pipe '{_pipeName}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SuggestionServiceServer] CRITICAL ERROR: Failed to create named pipe '{_pipeName}': {ex.Message}");
            _isRunning = false;
            throw;
        }
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
        Console.WriteLine($"[SuggestionServiceServer] Starting listening loop for pipe '{_pipeName}'...");
        
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                Console.WriteLine($"[SuggestionServiceServer] Waiting for client connection on pipe '{_pipeName}'...");
                
                // If no pipe server exists, create one
                if (_pipeServer == null)
                {
                    _pipeServer = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        1, // Single connection
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                }

                // Wait for client connection
                await _pipeServer.WaitForConnectionAsync(cancellationToken);
                Console.WriteLine($"[SuggestionServiceServer] Client connected to pipe '{_pipeName}'");

                // Create JsonRpc instance
                _rpc = new JsonRpc(_pipeServer);
                _rpc.AddLocalRpcTarget(_service);
                _rpc.StartListening();
                Console.WriteLine($"[SuggestionServiceServer] JsonRpc started for pipe '{_pipeName}'");

                // Wait for the connection to close
                await _rpc.Completion;
                Console.WriteLine($"[SuggestionServiceServer] Client disconnected from pipe '{_pipeName}'");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"[SuggestionServiceServer] Server shutdown requested for pipe '{_pipeName}'");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SuggestionServiceServer] ERROR in listening loop for pipe '{_pipeName}': {ex.Message}");
                Console.WriteLine($"[SuggestionServiceServer] Exception type: {ex.GetType().Name}");
                // Simple error handling - just continue listening
                await Task.Delay(1000, cancellationToken);
            }
            finally
            {
                // Clean up
                Console.WriteLine($"[SuggestionServiceServer] Cleaning up connection for pipe '{_pipeName}'");
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
        
        Console.WriteLine($"[SuggestionServiceServer] Listening loop ended for pipe '{_pipeName}'");
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
