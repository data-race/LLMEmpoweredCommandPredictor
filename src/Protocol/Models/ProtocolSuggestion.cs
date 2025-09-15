using System;

namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Serializable suggestion model for JSON-RPC communication.
/// This model can be safely serialized/deserialized across the protocol boundary.
/// </summary>
public class ProtocolSuggestion
{
    /// <summary>
    /// The suggested command text
    /// </summary>
    public string SuggestionText { get; set; } = string.Empty;

    /// <summary>
    /// Optional tooltip text for the suggestion
    /// </summary>
    public string? ToolTip { get; set; }

    /// <summary>
    /// Parameterless constructor for JSON serialization
    /// </summary>
    public ProtocolSuggestion()
    {
    }

    /// <summary>
    /// Constructor with suggestion text
    /// </summary>
    /// <param name="suggestionText">The suggested command text</param>
    /// <param name="toolTip">Optional tooltip text</param>
    public ProtocolSuggestion(string suggestionText, string? toolTip = null)
    {
        SuggestionText = suggestionText ?? throw new ArgumentNullException(nameof(suggestionText));
        ToolTip = toolTip;
    }
}