namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Configuration settings for the IPC connection between client and server.
/// </summary>
public class ConnectionSettings
{
    /// <summary>
    /// RPC call timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 15;

    /// <summary>
    /// Connection establishment timeout in milliseconds.
    /// </summary>
    public int ConnectionTimeoutMs { get; init; } = 1000;

    /// <summary>
    /// Maximum number of retry attempts for failed connections.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Delay between retry attempts in milliseconds.
    /// </summary>
    public int RetryDelayMs { get; init; } = 100;

    /// <summary>
    /// Whether to enable detailed logging for debugging purposes.
    /// </summary>
    public bool EnableDebugLogging { get; init; } = false;
}
