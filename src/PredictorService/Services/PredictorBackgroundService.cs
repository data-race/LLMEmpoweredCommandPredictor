using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LLMEmpoweredCommandPredictor.PredictorService.Context;
using System.Text.Json;

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

    public PredictorBackgroundService(
        ILogger<PredictorBackgroundService> logger,
        ContextManager contextManager,
        AzureOpenAIService? azureOpenAIService = null,
        PromptTemplateService? promptTemplateService = null)
    {
        _logger = logger;
        _contextManager = contextManager;
        _azureOpenAIService = azureOpenAIService;
        _promptTemplateService = promptTemplateService;
    }

    /// <summary>
    /// Starts the background service and runs a simple loop with context collection demos.
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

            int counter = 0;
            
            // Keep the service running until cancellation is requested
            while (!stoppingToken.IsCancellationRequested)
            {
                counter++;
                
                // Demonstrate context collection every 60 seconds
                if (counter % 60 == 0)
                {
                    await CollectContextAndPredictCommandsAsync(stoppingToken);
                }
                
                // Log periodic status every 30 seconds
                if (counter % 30 == 0)
                {
                    _logger.LogInformation("Service is running... Uptime: {Counter} seconds", counter);
                }
                
                await Task.Delay(1000, stoppingToken);
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
            _contextManager.Dispose();
        }
    }

    /// <summary>
    /// Demonstrates context collection functionality.
    /// </summary>
    private async Task CollectContextAndPredictCommandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Demonstrating context collection and LLM integration...");

            // Collect context for a sample user input
            var context = await _contextManager.CollectContextAsync("Get-Process", cancellationToken);
            
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

            // Simulate updating context with a new command
            await _contextManager.UpdateContextAsync(context, "Get-Process -Name powershell", true, cancellationToken);
            
            _logger.LogInformation("Context updated with simulated command execution");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during context collection demonstration");
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
                    }
                    catch
                    {
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
    /// Handles graceful shutdown of the background service.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Predictor Background Service stop requested");
        
        try
        {
            _contextManager.Dispose();
            await base.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during service shutdown");
        }
    }
}
