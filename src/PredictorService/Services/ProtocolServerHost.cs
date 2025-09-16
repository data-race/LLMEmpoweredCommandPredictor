using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LLMEmpoweredCommandPredictor.Protocol.Factory;
using LLMEmpoweredCommandPredictor.Protocol.Integration;
using LLMEmpoweredCommandPredictor.Protocol.Contracts;

namespace LLMEmpoweredCommandPredictor.PredictorService.Services;

public class ProtocolServerHost : BackgroundService
{
    private readonly ILogger<ProtocolServerHost> _logger;
    private readonly ISuggestionService _suggestionService;

    public ProtocolServerHost(ILogger<ProtocolServerHost> logger, ISuggestionService suggestionService)
    {
        _logger = logger;
        _suggestionService = suggestionService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Protocol server with integrated caching...");
        var server = ProtocolFactory.CreateServer(_suggestionService);
        
        try
        {
            await server.StartAsync();
            _logger.LogInformation("Protocol server started");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Protocol server error");
        }
        finally
        {
            server?.Dispose();
            _logger.LogInformation("Protocol server stopped");
        }
    }
}
