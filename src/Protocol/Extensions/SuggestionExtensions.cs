using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Subsystem.Prediction;
using LLMEmpoweredCommandPredictor.Protocol.Models;

namespace LLMEmpoweredCommandPredictor.Protocol.Extensions;

/// <summary>
/// Extension methods for converting between PowerShell PredictiveSuggestion and DTO types
/// </summary>
public static class SuggestionExtensions
{
    /// <summary>
    /// Converts a list of PowerShell PredictiveSuggestion to DTO list
    /// </summary>
    public static IReadOnlyList<PredictiveSuggestionDto> ToDto(this IEnumerable<PredictiveSuggestion> suggestions)
    {
        return suggestions?.Select(s => new PredictiveSuggestionDto(s.SuggestionText)).ToList() ?? new List<PredictiveSuggestionDto>();
    }

    /// <summary>
    /// Converts a list of DTO to PowerShell PredictiveSuggestion list
    /// </summary>
    public static IList<PredictiveSuggestion> ToPowerShell(this IEnumerable<PredictiveSuggestionDto> dtos)
    {
        return dtos?.Select(dto => new PredictiveSuggestion(dto.SuggestionText)).ToList() ?? new List<PredictiveSuggestion>();
    }
}
