using System;

namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Serializable DTO for PredictiveSuggestion to enable JSON-RPC communication.
/// This is a simple wrapper around the suggestion text that can be serialized/deserialized.
/// </summary>
public class PredictiveSuggestionDto
{
    /// <summary>
    /// The suggestion text
    /// </summary>
    public string SuggestionText { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new predictive suggestion DTO
    /// </summary>
    public PredictiveSuggestionDto() { }

    /// <summary>
    /// Creates a new predictive suggestion DTO with the specified text
    /// </summary>
    /// <param name="suggestionText">The suggestion text</param>
    public PredictiveSuggestionDto(string suggestionText)
    {
        SuggestionText = suggestionText ?? string.Empty;
    }
}
