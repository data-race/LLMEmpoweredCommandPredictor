using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LLMEmpoweredCommandPredictor.PredictorService.Services;
using LLMEmpoweredCommandPredictor.PredictorService.Context;

namespace LLMEmpoweredCommandPredictor.PredictorService;

/// <summary>
/// Entry point for the LLM Empowered Command Predictor Background Service.
/// This is a standalone executable that runs independently of PowerShell.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting LLM Empowered Command Predictor Service...");

        var host = CreateHostBuilder(args).Build();

        try
        {
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service failed to start: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Creates and configures the host builder for the background service.
    /// </summary>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                // Register context providers
                services.AddSingleton<CommandHistoryContextProvider>();
                
                // Register context manager and configure it
                services.AddSingleton<ContextManager>(serviceProvider =>
                {
                    var logger = serviceProvider.GetRequiredService<ILogger<ContextManager>>();
                    var contextManager = new ContextManager(logger);
                    
                    // Register the command history provider
                    var historyProvider = serviceProvider.GetRequiredService<CommandHistoryContextProvider>();
                    contextManager.RegisterProvider(historyProvider);
                    
                    return contextManager;
                });
                
                // Register the background service
                services.AddHostedService<PredictorBackgroundService>();
                
                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .UseConsoleLifetime();
}
