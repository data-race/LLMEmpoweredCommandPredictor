using System;
using System.Collections.Generic;

namespace LLMEmpoweredCommandPredictor;

public class LLMSuggestionContext
{
    /// <summary>
    /// The current user input
    /// </summary>
    public string UserInput { get; set; }

    /// <summary>
    /// Recent command history
    /// </summary>
    public IReadOnlyList<string> CommandHistory { get; set; }

    /// <summary>
    /// Current working directory
    /// </summary>
    public string WorkingDirectory { get; set; }
}
