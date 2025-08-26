using System;
using System.Collections.Generic;

namespace LLMEmpoweredCommandPredictor.PredictorService.Context;

/// <summary>
/// Represents the context information collected for generating command suggestions.
/// This class holds all relevant information about the user's current environment and history.
/// </summary>
public class PredictorContext
{
    /// <summary>
    /// The current user input that needs suggestions for
    /// </summary>
    public string UserInput { get; set; } = string.Empty;

    /// <summary>
    /// Current working directory
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Recent command history (most recent first)
    /// </summary>
    public IReadOnlyList<CommandHistoryEntry> CommandHistory { get; set; } = Array.Empty<CommandHistoryEntry>();

    /// <summary>
    /// Timestamp when this context was collected
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Session identifier for grouping related commands
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// PowerShell version information
    /// </summary>
    public string PowerShellVersion { get; set; } = string.Empty;

    /// <summary>
    /// Operating system information
    /// </summary>
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>
    /// Gets a summary of the context for logging purposes
    /// </summary>
    public string GetSummary()
    {
        return $"UserInput: '{UserInput}', WorkingDir: '{WorkingDirectory}', " +
               $"HistoryCount: {CommandHistory.Count}, Session: {SessionId}";
    }
}

/// <summary>
/// Represents a single entry in the command history
/// </summary>
public class CommandHistoryEntry
{
    /// <summary>
    /// The command that was executed
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// When the command was executed
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Whether the command executed successfully
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Working directory when the command was executed
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Duration of command execution (if available)
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Exit code of the command (if available)
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// Creates a new command history entry
    /// </summary>
    public CommandHistoryEntry(string command, DateTime executedAt, bool isSuccessful = true)
    {
        Command = command;
        ExecutedAt = executedAt;
        IsSuccessful = isSuccessful;
    }

    /// <summary>
    /// Default constructor for serialization
    /// </summary>
    public CommandHistoryEntry() { }

    public override string ToString()
    {
        return $"{ExecutedAt:HH:mm:ss} [{(IsSuccessful ? "✓" : "✗")}] {Command}";
    }
}
