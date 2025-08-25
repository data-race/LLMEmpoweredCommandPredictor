namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Service status information for basic health checking.
/// </summary>
public class ServiceStatus
{
    /// <summary>
    /// Whether the service is currently running and responsive
    /// </summary>
    public bool IsRunning { get; init; }

    /// <summary>
    /// Service uptime
    /// </summary>
    public TimeSpan Uptime { get; init; }
}
