using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LLMEmpoweredCommandPredictor.Protocol.Factory;
using LLMEmpoweredCommandPredictor.Protocol.Integration;

namespace LLMEmpoweredCommandPredictor.PredictorService.Services;

public class ProtocolServerHost : BackgroundService
{
    private readonly ILogger<ProtocolServerHost> _logger;
    private readonly ServiceBridge _serviceBridge;

    public ProtocolServerHost(ILogger<ProtocolServerHost> logger, ServiceBridge serviceBridge)
    {
        _logger = logger;
        _serviceBridge = serviceBridge;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Protocol server");
        
        var server = ProtocolFactory.CreateServer(_serviceBridge);
        
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
