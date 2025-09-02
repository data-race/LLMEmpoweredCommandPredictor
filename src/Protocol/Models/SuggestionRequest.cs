using System.Collections.Generic;

namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Request model for getting command suggestions.
/// Contains the essential context information needed to generate relevant suggestions.
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
}
