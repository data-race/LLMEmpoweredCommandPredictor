using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LLMEmpoweredCommandPredictor.PredictorService.Services;
using LLMEmpoweredCommandPredictor.PredictorService.Context;
using LLMEmpoweredCommandPredictor.PredictorService.Configuration;

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

                // Configure Azure OpenAI settings (can be overridden by environment variables)
                var azureOpenAIConfig = new AzureOpenAIConfiguration
                {
                    Endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? "https://yongyu-chatgpt-test1.openai.azure.com/",
                    DeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gtp-4.1",
                    ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY") ?? string.Empty
                };

                // Configure prompt template settings
                var promptConfig = new PromptConfiguration
                {
                    TemplatePath = Path.GetFullPath(Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "..", "..", "..", "..", "Prompt", "LLMSuggestionPromptTemplateV1.txt"))
                };

                // Register Azure OpenAI service if configuration is valid
                if (azureOpenAIConfig.IsValid())
                {
                    services.AddSingleton(provider =>
                        new AzureOpenAIService(
                            provider.GetRequiredService<ILogger<AzureOpenAIService>>(),
                            azureOpenAIConfig.Endpoint,
                            azureOpenAIConfig.DeploymentName,
                            azureOpenAIConfig.ApiKey));
                }
                else
                {
                    Console.WriteLine("Azure OpenAI configuration not provided. Set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT environment variables to enable LLM features.");
                }

                // Register prompt template service if template exists
                if (promptConfig.IsValid())
                {
                    services.AddSingleton(provider =>
                        new PromptTemplateService(
                            provider.GetRequiredService<ILogger<PromptTemplateService>>(),
                            promptConfig.TemplatePath));
                }
                else
                {
                    Console.WriteLine($"Prompt template not found at: {promptConfig.TemplatePath}");
                }
                
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
