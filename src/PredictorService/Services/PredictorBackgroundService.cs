using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LLMEmpoweredCommandPredictor.PredictorService.Context;

namespace LLMEmpoweredCommandPredictor.PredictorService.Services;

/// <summary>
/// Simple background service that runs continuously and demonstrates context collection.
/// This is a placeholder implementation that will be expanded with actual functionality.
/// </summary>
public class PredictorBackgroundService : BackgroundService
{
    private readonly ILogger<PredictorBackgroundService> _logger;
    private readonly ContextManager _contextManager;

    public PredictorBackgroundService(
        ILogger<PredictorBackgroundService> logger,
        ContextManager contextManager)
    {
        _logger = logger;
        _contextManager = contextManager;
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
                    await DemonstrateContextCollectionAsync(stoppingToken);
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
    private async Task DemonstrateContextCollectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Demonstrating context collection...");

            // Collect context for a sample user input
            var context = await _contextManager.CollectContextAsync("Get-Process", cancellationToken);
            
            _logger.LogInformation("Context collected: {Summary}", context.GetSummary());
            _logger.LogInformation("Command history contains {Count} entries", context.CommandHistory.Count);

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
