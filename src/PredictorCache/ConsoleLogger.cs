using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace LLMEmpoweredCommandPredictor.PredictorCache;

/// <summary>
/// Simple console logger for debugging cache operations in PowerShell
/// </summary>
public class ConsoleLogger<T> : ILogger<T>
{
    private readonly string categoryName;
    private readonly LogLevel minimumLevel;
    private readonly string logFilePath;

    public ConsoleLogger(LogLevel minimumLevel = LogLevel.Information, string? customFileName = null)
    {
        this.categoryName = typeof(T).Name;
        this.minimumLevel = minimumLevel;
        
        // Create log file in temp directory
        var tempDir = Path.GetTempPath();
        var fileName = customFileName ?? "LLMCommandPredictor.log";
        this.logFilePath = Path.Combine(tempDir, fileName);
        
        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(this.logFilePath)!);
    }

    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var levelString = GetLevelString(logLevel);
        var message = formatter(state, exception);
        
        var logEntry = $"[{timestamp}] [{levelString}] [{categoryName}] {message}";
        
        try
        {
            // Always write to file
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            
            // Only write to console for non-DEBUG levels
            if (logLevel != LogLevel.Debug)
            {
                Console.WriteLine(logEntry);
            }
            
            if (exception != null)
            {
                var exceptionEntry = $"[{timestamp}] [{levelString}] [{categoryName}] Exception: {exception}";
                File.AppendAllText(logFilePath, exceptionEntry + Environment.NewLine);
                
                // Only write exception to console for non-DEBUG levels
                if (logLevel != LogLevel.Debug)
                {
                    Console.WriteLine(exceptionEntry);
                }
            }
        }
        catch
        {
            // Ignore file write errors to prevent logging from breaking the application
        }
    }

    private static string GetLevelString(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT ",
        _ => "UNKN "
    };

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}

/// <summary>
/// Factory for creating console loggers
/// </summary>
public static class ConsoleLoggerFactory
{
    /// <summary>
    /// Creates a console logger for debugging cache operations
    /// Set to Debug level to see detailed cache operations
    /// </summary>
    public static ILogger<T> CreateDebugLogger<T>() => new ConsoleLogger<T>(LogLevel.Debug);
    
    /// <summary>
    /// Creates a console logger for general information
    /// </summary>
    public static ILogger<T> CreateInfoLogger<T>() => new ConsoleLogger<T>(LogLevel.Information);
    
    /// <summary>
    /// Creates a cache-specific logger that writes to a separate file
    /// Logs to $env:TEMP\LLMCommandPredictor_Cache.log
    /// </summary>
    public static ILogger<T> CreateCacheLogger<T>() => new ConsoleLogger<T>(LogLevel.Debug, "LLMCommandPredictor_Cache.log");
}
