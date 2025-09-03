using System;

namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Comprehensive service status and performance metrics.
/// Provides detailed information about service health, performance, and operational statistics.
/// </summary>
public class ServiceStatus
{
    /// <summary>
    /// Whether the service is currently running and responsive
    /// </summary>
    public bool IsRunning { get; set; } = true;

    /// <summary>
    /// How long the service has been running
    /// </summary>
    public TimeSpan Uptime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Number of cached suggestions currently available
    /// </summary>
    public int CachedSuggestionsCount { get; set; } = 0;

    /// <summary>
    /// Number of requests processed since service start
    /// </summary>
    public long TotalRequestsProcessed { get; set; } = 0;

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AverageResponseTimeMs { get; set; } = 0.0;

    /// <summary>
    /// Last time the cache was updated
    /// </summary>
    public DateTimeOffset LastCacheUpdate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Service version information
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Any error messages or warnings
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when this status was generated
    /// </summary>
    public DateTimeOffset StatusTimestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Health score from 0.0 (unhealthy) to 1.0 (healthy)
    /// </summary>
    public double HealthScore { get; set; } = 1.0;

    /// <summary>
    /// Memory usage in megabytes
    /// </summary>
    public double MemoryUsageMb { get; set; } = 0.0;

    /// <summary>
    /// CPU usage percentage
    /// </summary>
    public double CpuUsagePercent { get; set; } = 0.0;

    /// <summary>
    /// Number of active client connections
    /// </summary>
    public int ActiveConnections { get; set; } = 0;

    /// <summary>
    /// Creates a new service status instance with default values
    /// </summary>
    public ServiceStatus() { }

    /// <summary>
    /// Creates a new service status instance with custom values
    /// </summary>
    public ServiceStatus(
        bool isRunning = true,
        TimeSpan uptime = default,
        int cachedSuggestionsCount = 0,
        long totalRequestsProcessed = 0,
        double averageResponseTimeMs = 0.0,
        DateTimeOffset lastCacheUpdate = default,
        string version = "1.0.0",
        string? errorMessage = null,
        DateTimeOffset statusTimestamp = default,
        double healthScore = 1.0,
        double memoryUsageMb = 0.0,
        double cpuUsagePercent = 0.0,
        int activeConnections = 0)
    {
        IsRunning = isRunning;
        Uptime = uptime;
        CachedSuggestionsCount = cachedSuggestionsCount;
        TotalRequestsProcessed = totalRequestsProcessed;
        AverageResponseTimeMs = averageResponseTimeMs;
        LastCacheUpdate = lastCacheUpdate;
        Version = version;
        ErrorMessage = errorMessage;
        StatusTimestamp = statusTimestamp;
        HealthScore = healthScore;
        MemoryUsageMb = memoryUsageMb;
        CpuUsagePercent = cpuUsagePercent;
        ActiveConnections = activeConnections;
    }
}
