using System;
using System.Collections.Generic;

namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Request model for getting command suggestions.
/// Contains comprehensive context information needed to generate relevant suggestions.
/// </summary>
public class SuggestionRequest
{
    /// <summary>
    /// The current user input that needs suggestions for
    /// </summary>
    public string UserInput { get; set; } = string.Empty;

    /// <summary>
    /// Current working directory to provide file system context
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of suggestions to return
    /// </summary>
    public int MaxSuggestions { get; set; } = 5;

    /// <summary>
    /// Recent command history for context-aware suggestions
    /// </summary>
    public IReadOnlyList<string> CommandHistory { get; set; } = new List<string>();

    /// <summary>
    /// PowerShell version information
    /// </summary>
    public string PowerShellVersion { get; set; } = string.Empty;

    /// <summary>
    /// Operating system information
    /// </summary>
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>
    /// User session identifier for personalization
    /// </summary>
    public string UserSessionId { get; set; } = string.Empty;

    /// <summary>
    /// Priority level for this request (higher = more important)
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// Timestamp when this request was created
    /// </summary>
    public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a new suggestion request with default values
    /// </summary>
    public SuggestionRequest() { }

    /// <summary>
    /// Creates a new suggestion request with custom values
    /// </summary>
    public SuggestionRequest(
        string userInput,
        string workingDirectory = "",
        int maxSuggestions = 5,
        IReadOnlyList<string>? commandHistory = null,
        string powerShellVersion = "",
        string operatingSystem = "",
        string userSessionId = "",
        int priority = 1)
    {
        UserInput = userInput ?? string.Empty;
        WorkingDirectory = workingDirectory ?? string.Empty;
        MaxSuggestions = maxSuggestions;
        CommandHistory = commandHistory ?? new List<string>();
        PowerShellVersion = powerShellVersion ?? string.Empty;
        OperatingSystem = operatingSystem ?? string.Empty;
        UserSessionId = userSessionId ?? string.Empty;
        Priority = priority;
        RequestTimestamp = DateTime.UtcNow;
    }
}
