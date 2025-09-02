using System.Collections.Generic;
using System.Management.Automation.Subsystem.Prediction;

namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Response model containing suggestions and metadata.
/// Provides essential information about the suggestion generation process.
/// </summary>
public class SuggestionResponse
{
    /// <summary>
    /// List of generated suggestions
    /// </summary>
    public IReadOnlyList<PredictiveSuggestion> Suggestions { get; set; } = new List<PredictiveSuggestion>();

    /// <summary>
    /// Source of suggestions (e.g., "cache", "llm", "fallback")
    /// </summary>
    public string Source { get; set; } = "unknown";

    /// <summary>
    /// Confidence score for the suggestions (0.0 to 1.0)
    /// </summary>
    public double ConfidenceScore { get; set; } = 1.0;

    /// <summary>
    /// Any warnings or additional information
    /// </summary>
    public string? WarningMessage { get; set; }
}
