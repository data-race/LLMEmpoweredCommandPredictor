using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LLMEmpoweredCommandPredictor.PredictorService.Services;
using LLMEmpoweredCommandPredictor.PredictorService.Context;
using LLMEmpoweredCommandPredictor.PredictorService.Configuration;
using LLMEmpoweredCommandPredictor.Protocol.Factory;
using LLMEmpoweredCommandPredictor.Protocol.Contracts;
using LLMEmpoweredCommandPredictor.PredictorCache;

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
                services.AddSingleton<SessionHistoryContextProvider>();
                
                // Register context manager and configure it
                services.AddSingleton<ContextManager>(serviceProvider =>
                {
                    var logger = serviceProvider.GetRequiredService<ILogger<ContextManager>>();
                    var contextManager = new ContextManager(logger);
                    
                    // Register the global command history provider
                    var historyProvider = serviceProvider.GetRequiredService<CommandHistoryContextProvider>();
                    contextManager.RegisterProvider(historyProvider);
                    
                    // Register the session history provider
                    var sessionHistoryProvider = serviceProvider.GetRequiredService<SessionHistoryContextProvider>();
                    contextManager.RegisterProvider(sessionHistoryProvider);
                    
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
                
                // Register Cache services with cache-specific file logging
                services.AddSingleton<InMemoryCache>(provider =>
                {
                    // Use cache-specific file logger
                    var logger = ConsoleLoggerFactory.CreateCacheLogger<InMemoryCache>();
                    var cache = new InMemoryCache(new CacheConfiguration(), logger);
                    
                    // Force cache initialization by requesting it (this ensures the constructor runs)
                    logger.LogInformation("Cache service registered and initialized");
                    
                    return cache;
                });
                services.AddSingleton<CacheKeyGenerator>(provider =>
                {
                    // Use cache-specific file logger
                    var logger = ConsoleLoggerFactory.CreateCacheLogger<CacheKeyGenerator>();
                    return new CacheKeyGenerator(logger);
                });
                
                // Register PredictorServiceBackend directly as ISuggestionService for IPC
                // This creates the correct architecture: IPC -> PredictorServiceBackend -> Cache
                services.AddSingleton<ISuggestionService>(provider =>
                {
                    // Use shared file logger for backend
                    var logger = ConsoleLoggerFactory.CreateInfoLogger<PredictorServiceBackend>();
                    var contextManager = provider.GetRequiredService<ContextManager>();
                    var cache = provider.GetRequiredService<InMemoryCache>();
                    var keyGenerator = provider.GetRequiredService<CacheKeyGenerator>();
                    var azureOpenAI = provider.GetService<AzureOpenAIService>();
                    var promptTemplate = provider.GetService<PromptTemplateService>();
                    
                    return new PredictorServiceBackend(logger, contextManager, cache, keyGenerator, azureOpenAI, promptTemplate);
                });
                
                services.AddHostedService<ProtocolServerHost>();
                
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
