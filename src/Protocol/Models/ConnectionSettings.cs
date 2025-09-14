using System;

namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Configuration settings for IPC connection behavior.
/// Controls timeouts, retries, and connection management.
/// </summary>
public class ConnectionSettings
{
    /// <summary>
    /// Timeout for individual RPC calls in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; } = 200;

    /// <summary>
    /// Timeout for establishing initial connection in milliseconds
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// Maximum number of retry attempts for failed operations
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 100;

    /// <summary>
    /// Whether to enable debug logging for connection operations
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;


    /// <summary>
    /// Creates a new connection settings instance with default values
    /// </summary>
    public ConnectionSettings() { }

    /// <summary>
    /// Creates a new connection settings instance with custom values
    /// </summary>
    public ConnectionSettings(
        int timeoutMs = 200,
        int connectionTimeoutMs = 1000,
        int maxRetries = 3,
        int retryDelayMs = 100,
        bool enableDebugLogging = false)
    {
        TimeoutMs = timeoutMs;
        ConnectionTimeoutMs = connectionTimeoutMs;
        MaxRetries = maxRetries;
        RetryDelayMs = retryDelayMs;
        EnableDebugLogging = enableDebugLogging;
    }
}
