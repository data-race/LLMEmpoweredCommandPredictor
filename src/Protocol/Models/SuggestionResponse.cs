using System;
using System.Collections.Generic;

namespace LLMEmpoweredCommandPredictor.Protocol.Models;

/// <summary>
/// Response model containing suggestions and comprehensive metadata.
/// Provides detailed information about the suggestion generation process and results.
/// </summary>
public class SuggestionResponse
{
    /// <summary>
    /// List of generated suggestions
    /// </summary>
    public IReadOnlyList<ProtocolSuggestion> Suggestions { get; set; } = new List<ProtocolSuggestion>();

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

    /// <summary>
    /// Timestamp when suggestions were generated
    /// </summary>
    public DateTime GeneratedTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when suggestions were retrieved from cache (if applicable)
    /// </summary>
    public DateTime? CachedTimestamp { get; set; }

    /// <summary>
    /// Whether these suggestions came from cache
    /// </summary>
    public bool IsFromCache { get; set; } = false;

    /// <summary>
    /// Time taken to generate suggestions in milliseconds
    /// </summary>
    public double GenerationTimeMs { get; set; } = 0.0;

    /// <summary>
    /// Cache hit rate percentage (0.0 to 100.0)
    /// </summary>
    public double CacheHitRate { get; set; } = 0.0;

    /// <summary>
    /// Correlation ID to link this response with the original request
    /// </summary>
    public string? RequestCorrelationId { get; set; }

    /// <summary>
    /// Server-side timestamp when this response was created
    /// </summary>
    public DateTime ServerCreatedTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a new suggestion response with default values
    /// </summary>
    public SuggestionResponse() { }

    /// <summary>
    /// Creates a new suggestion response with custom values
    /// </summary>
    public SuggestionResponse(
        IReadOnlyList<ProtocolSuggestion> suggestions,
        string source = "unknown",
        double confidenceScore = 1.0,
        string? warningMessage = null,
        DateTime? generatedTimestamp = null,
        DateTime? cachedTimestamp = null,
        bool isFromCache = false,
        double generationTimeMs = 0.0,
        double cacheHitRate = 0.0,
        string? requestCorrelationId = null)
    {
        Suggestions = suggestions ?? new List<ProtocolSuggestion>();
        Source = source ?? "unknown";
        ConfidenceScore = confidenceScore;
        WarningMessage = warningMessage;
        GeneratedTimestamp = generatedTimestamp ?? DateTime.UtcNow;
        CachedTimestamp = cachedTimestamp;
        IsFromCache = isFromCache;
        GenerationTimeMs = generationTimeMs;
        CacheHitRate = cacheHitRate;
        RequestCorrelationId = requestCorrelationId;
        ServerCreatedTimestamp = DateTime.UtcNow;
    }
}
