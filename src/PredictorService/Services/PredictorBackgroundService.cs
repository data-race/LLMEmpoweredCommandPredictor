using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LLMEmpoweredCommandPredictor.PredictorService.Context;
using LLMEmpoweredCommandPredictor.PredictorService.Cache;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO.Pipes;
using System.Text;
using System.IO;

namespace LLMEmpoweredCommandPredictor.PredictorService.Services;

/// <summary>
/// Simple background service that runs continuously and demonstrates context collection.
/// This is a placeholder implementation that will be expanded with actual functionality.
/// </summary>
public class PredictorBackgroundService : BackgroundService
{
    private readonly ILogger<PredictorBackgroundService> _logger;
    private readonly ContextManager _contextManager;
    private readonly AzureOpenAIService? _azureOpenAIService;
    private readonly PromptTemplateService? _promptTemplateService;
    private readonly SimpleMemCache _cache;
    private readonly string _hashFilePath;
    private FileSystemWatcher? _hashFileWatcher;

    public PredictorBackgroundService(
        ILogger<PredictorBackgroundService> logger,
        ContextManager contextManager,
        SimpleMemCache cache,
        AzureOpenAIService? azureOpenAIService = null,
        PromptTemplateService? promptTemplateService = null)
    {
        _logger = logger;
        _contextManager = contextManager;
        _cache = cache;
        _azureOpenAIService = azureOpenAIService;
        _promptTemplateService = promptTemplateService;
        
        // Initialize hash file path - same as PowerShell script uses
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var llmPredictorPath = Path.Combine(appDataPath, "LLMCommandPredictor");
        _hashFilePath = Path.Combine(llmPredictorPath, "Get-History-Sign.txt");
    }

    /// <summary>
    /// Starts the background service and sets up file watching for hash changes.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Predictor Background Service starting...");
        _logger.LogInformation("Hello World from LLM Empowered Command Predictor Service!");

        try
        {
            // Initialize the context manager
            await _contextManager.InitializeAsync(stoppingToken);
            _logger.LogInformation("Context Manager initialized successfully");

            // Start the named pipe server task
            var namedPipeTask = Task.Run(() => StartNamedPipeServerAsync(stoppingToken), stoppingToken);

            // Set up file system watcher for hash file changes
            SetupHashFileWatcher();

            // Generate initial suggestions on startup
            _logger.LogInformation("Generating initial LLM suggestions on startup...");
            await GenerateSuggestionsFromContextAsync(stoppingToken);

            // Keep the service running until cancellation is requested
            while (!stoppingToken.IsCancellationRequested)
            {
                // Log periodic status every 10 seconds
                await Task.Delay(10000, stoppingToken);
                _logger.LogInformation("Predictor Background Service is running. Cache size: {CacheSize}", _cache.Count);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Predictor Background Service shutdown requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Predictor Background Service");
            throw;
        }
        finally
        {
            _logger.LogInformation("Predictor Background Service stopping...");
            _hashFileWatcher?.Dispose();
            _contextManager.Dispose();
        }
    }

    /// <summary>
    /// Sets up file system watcher to monitor changes to the hash file.
    /// </summary>
    private void SetupHashFileWatcher()
    {
        try
        {
            var hashFileDirectory = Path.GetDirectoryName(_hashFilePath);
            if (string.IsNullOrEmpty(hashFileDirectory))
            {
                _logger.LogError("Invalid hash file path: {HashFilePath}", _hashFilePath);
                return;
            }

            // Ensure the directory exists
            if (!Directory.Exists(hashFileDirectory))
            {
                Directory.CreateDirectory(hashFileDirectory);
                _logger.LogInformation("Created directory for hash file: {Directory}", hashFileDirectory);
            }

            _hashFileWatcher = new FileSystemWatcher(hashFileDirectory, "Get-History-Sign.txt")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            _hashFileWatcher.Changed += OnHashFileChanged;
            _hashFileWatcher.Created += OnHashFileChanged;

            _logger.LogInformation("File system watcher set up for hash file: {HashFilePath}", _hashFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up file system watcher for hash file: {HashFilePath}", _hashFilePath);
        }
    }

    /// <summary>
    /// Handles hash file change events.
    /// </summary>
    private void OnHashFileChanged(object sender, FileSystemEventArgs e)
    {
        // Fire and forget async operation with proper error handling
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Hash file changed: {FilePath}", e.FullPath);
                
                // Add a small delay to ensure the file write is complete
                await Task.Delay(500);
                
                // Generate suggestions from current context
                await GenerateSuggestionsFromContextAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling hash file change event");
            }
        });
    }

    /// <summary>
    /// Generates LLM suggestions from current context.
    /// </summary>
    private async Task GenerateSuggestionsFromContextAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating LLM suggestions from current context...");

            // Collect context for a generic input to get recent history
            var context = await _contextManager.CollectContextAsync("", cancellationToken);
            
            _logger.LogInformation("Context collected: {Summary}", context.GetSummary());
            _logger.LogInformation("Command history contains {Count} entries", context.CommandHistory.Count);

            // If Azure OpenAI is configured, generate suggestions
            if (_azureOpenAIService != null && _promptTemplateService != null)
            {
                await GenerateLLMSuggestionsAsync(context, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Azure OpenAI service not configured. Skipping LLM suggestions.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating suggestions from context");
        }
    }

    /// <summary>
    /// Generates LLM suggestions using the collected context.
    /// </summary>
    private async Task GenerateLLMSuggestionsAsync(PredictorContext context, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating LLM suggestions...");

            // Merge context with prompt template
            var prompt = await _promptTemplateService!.MergeContextWithTemplateAsync(context);
            _logger.LogDebug("Prompt generated with {Length} characters", prompt.Length);

            // Call Azure OpenAI to get suggestions
            var response = await _azureOpenAIService!.GenerateCommandSuggestionsAsync(prompt, cancellationToken);

            // Log the response
            if (!string.IsNullOrWhiteSpace(response))
            {
                _logger.LogInformation("LLM Response received:");
                Console.WriteLine("=== LLM Command Suggestions ===");
                
                // Try to format as JSON for better readability
                if (_azureOpenAIService.IsValidJsonResponse(response))
                {
                    try
                    {
                        var jsonDocument = JsonDocument.Parse(response);
                        var formattedJson = JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        });
                        Console.WriteLine(formattedJson);

                        // Parse and store suggestions in cache
                        StoreSuggestionsInCache(response);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse JSON response for caching");
                        Console.WriteLine(response);
                    }
                }
                else
                {
                    Console.WriteLine(response);
                }
                
                Console.WriteLine("=== End of LLM Response ===");
                Console.WriteLine();
            }
            else
            {
                _logger.LogWarning("Empty response received from Azure OpenAI");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating LLM suggestions");
        }
    }

    /// <summary>
    /// Parses the JSON response and stores suggestions in the cache.
    /// </summary>
    private void StoreSuggestionsInCache(string jsonResponse)
    {
        try
        {
            var suggestions = JsonSerializer.Deserialize<List<LLMSuggestion>>(jsonResponse);
            _logger.LogInformation("parse suggestions: {0}", suggestions?.Count);
            if (suggestions != null && suggestions.Count > 0)
            {
                var storedCount = 0;
                foreach (var suggestion in suggestions)
                {
                    _logger.LogInformation("{0}, {1}, {2}", suggestion.Command, suggestion.Description, suggestion.Confidence);
                    if (suggestion.Command != null && suggestion.Command.Count > 0)
                    {
                        // Join command array into a single string
                        var commandString = string.Join(" ", suggestion.Command);

                        // Store in cache (AddOrUpdate handles deduplication with FIFO policy)
                        _cache.AddOrUpdate(commandString, suggestion.Description ?? "", suggestion.Confidence);
                        storedCount++;
                    }
                }

                _logger.LogInformation("Stored {Count} suggestions in cache. Total cache size: {CacheSize}",
                    storedCount, _cache.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing suggestions in cache");
        }
    }

    /// <summary>
    /// Represents a suggestion from the LLM response.
    /// </summary>
    private class LLMSuggestion
    {
        [JsonPropertyName("command")]
        public List<string>? Command { get; set; }
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }
    }

    /// <summary>
    /// Handles graceful shutdown of the background service.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Predictor Background Service stop requested");
        
        try
        {
            _hashFileWatcher?.Dispose();
            _contextManager.Dispose();
            await base.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during service shutdown");
        }
    }

    /// <summary>
    /// Starts the named pipe server to handle client cache queries.
    /// </summary>
    private async Task StartNamedPipeServerAsync(CancellationToken cancellationToken)
    {
        const string pipeName = "LLMCommandPredictorCache";
        _logger.LogInformation("Starting named pipe server: {PipeName}", pipeName);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var pipeServer = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                _logger.LogDebug("Waiting for client connection on pipe: {PipeName}", pipeName);
                
                // Wait for client connection
                await pipeServer.WaitForConnectionAsync(cancellationToken);
                _logger.LogDebug("Client connected to named pipe");

                // Handle client requests
                await HandleClientRequestsAsync(pipeServer, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Named pipe server shutting down");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in named pipe server");
                // Wait a bit before retrying
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Handles incoming client requests on the named pipe.
    /// </summary>
    private async Task HandleClientRequestsAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
    {
        try
        {
            while (pipeServer.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                // Read request from client
                var buffer = new byte[1024];
                var bytesRead = await pipeServer.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                
                if (bytesRead == 0)
                {
                    _logger.LogDebug("Client disconnected");
                    break;
                }

                var requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                _logger.LogDebug("Received request: {Request}", requestJson);

                // Process the request and get response
                var response = ProcessCacheQuery(requestJson);
                
                // Send response back to client
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await pipeServer.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
                await pipeServer.FlushAsync(cancellationToken);
                
                _logger.LogDebug("Sent response with {Length} bytes", responseBytes.Length);
            }
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Client disconnected or pipe broken");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client requests");
        }
    }

    /// <summary>
    /// Processes a cache query and returns the results as JSON.
    /// </summary>
    private string ProcessCacheQuery(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<CacheQueryRequest>(requestJson);
            if (request == null || string.IsNullOrWhiteSpace(request.Prefix))
            {
                return JsonSerializer.Serialize(new CacheQueryResponse
                {
                    Success = false,
                    Error = "Invalid request: prefix is required"
                });
            }

            // Search cache using prefix
            var maxResults = request.MaxResults > 0 ? request.MaxResults : 10;
            var results = _cache.SearchByPrefix(request.Prefix, maxResults);

            // Convert to response format
            var suggestions = results.Select(item => new CacheSuggestion
            {
                Command = item.Command,
                Description = item.Description,
                Confidence = item.Confidence,
                Generated = item.Generated
            }).ToList();

            var response = new CacheQueryResponse
            {
                Success = true,
                Suggestions = suggestions,
                TotalCacheSize = _cache.Count
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cache query");
            return JsonSerializer.Serialize(new CacheQueryResponse
            {
                Success = false,
                Error = $"Error processing request: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Represents a cache query request from a client.
    /// </summary>
    private class CacheQueryRequest
    {
        public string? Prefix { get; set; }
        public int MaxResults { get; set; } = 10;
    }

    /// <summary>
    /// Represents a cache query response to a client.
    /// </summary>
    private class CacheQueryResponse
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<CacheSuggestion>? Suggestions { get; set; }
        public int TotalCacheSize { get; set; }
    }

    /// <summary>
    /// Represents a suggestion in the response.
    /// </summary>
    private class CacheSuggestion
    {
        public string Command { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public DateTime Generated { get; set; }
    }
}
