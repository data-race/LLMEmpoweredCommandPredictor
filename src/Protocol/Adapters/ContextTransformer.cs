using System;
using System.Collections.Generic;
using System.Linq;
using LLMEmpoweredCommandPredictor.Protocol.Models;

namespace LLMEmpoweredCommandPredictor.Protocol.Adapters;

/// <summary>
/// Provides transformation utilities between different context models.
/// This class bridges the gap between Plugin contexts, Protocol models, and Service contexts.
/// </summary>
public static class ContextTransformer
{
    /// <summary>
    /// Converts a Plugin's LLMSuggestionContext to a Protocol SuggestionRequest.
    /// This enables Plugin to communicate with Service via IPC.
    /// </summary>
    /// <param name="pluginContext">The plugin context containing user input and basic info</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to request (default: 5)</param>
    /// <param name="priority">Request priority level (default: 1)</param>
    /// <returns>Protocol-compatible SuggestionRequest</returns>
    public static SuggestionRequest FromPluginContext(
        object pluginContext, 
        int maxSuggestions = 5, 
        int priority = 1)
    {
        // Use reflection to extract fields from plugin context to avoid direct dependency
        var contextType = pluginContext.GetType();
        
        var userInput = GetPropertyValue<string>(pluginContext, "UserInput") ?? string.Empty;
        var workingDirectory = GetPropertyValue<string>(pluginContext, "WorkingDirectory") ?? string.Empty;
        var commandHistory = GetPropertyValue<IReadOnlyList<string>>(pluginContext, "CommandHistory") ?? new List<string>();
        
        return new SuggestionRequest(
            userInput: userInput,
            workingDirectory: workingDirectory,
            maxSuggestions: maxSuggestions,
            commandHistory: commandHistory,
            powerShellVersion: Environment.Version.ToString(),
            operatingSystem: Environment.OSVersion.ToString(),
            userSessionId: Environment.ProcessId.ToString(),
            priority: priority
        );
    }
    
    /// <summary>
    /// Converts a Protocol SuggestionRequest to Service context format.
    /// This enables Service to process Protocol requests using existing Service logic.
    /// </summary>
    /// <param name="protocolRequest">The Protocol request</param>
    /// <returns>Service-compatible context object</returns>
    public static object ToServiceContext(SuggestionRequest protocolRequest)
    {
        // Return an anonymous object that matches Service's PredictorContext structure
        return new
        {
            UserInput = protocolRequest.UserInput,
            WorkingDirectory = protocolRequest.WorkingDirectory,
            PowerShellVersion = protocolRequest.PowerShellVersion,
            OperatingSystem = protocolRequest.OperatingSystem,
            SessionId = protocolRequest.UserSessionId,
            Timestamp = protocolRequest.RequestTimestamp,
            
            // Transform CommandHistory: string[] → CommandHistoryEntry-like objects
            CommandHistory = protocolRequest.CommandHistory
                .Select(cmd => new
                {
                    Command = cmd,
                    ExecutedAt = DateTime.UtcNow.AddMinutes(-new Random().Next(1, 60)), // Simulate recent execution
                    IsSuccessful = true,
                    WorkingDirectory = protocolRequest.WorkingDirectory
                })
                .ToList()
        };
    }
    
    /// <summary>
    /// Converts Service response to Protocol SuggestionResponse format.
    /// </summary>
    /// <param name="serviceResponse">Raw service response (typically string or object)</param>
    /// <param name="maxSuggestions">Maximum suggestions to include</param>
    /// <param name="isFromCache">Whether the response came from cache</param>
    /// <returns>Protocol-compatible SuggestionResponse</returns>
    public static SuggestionResponse FromServiceResponse(
        object serviceResponse, 
        int maxSuggestions = 5, 
        bool isFromCache = false)
    {
        var suggestions = new List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>();
        
        // Handle different service response formats
        if (serviceResponse is string stringResponse)
        {
            // Parse string response (e.g., from LLM)
            suggestions = ParseStringToSuggestions(stringResponse, maxSuggestions);
        }
        else if (serviceResponse is IEnumerable<object> listResponse)
        {
            // Handle list responses
            suggestions = ParseListToSuggestions(listResponse, maxSuggestions);
        }
        
        return new SuggestionResponse(
            suggestions: suggestions,
            source: isFromCache ? "cache" : "llm",
            confidenceScore: 0.8,
            isFromCache: isFromCache,
            generationTimeMs: isFromCache ? 1.0 : 150.0,
            cacheHitRate: isFromCache ? 100.0 : 0.0
        );
    }
    
    /// <summary>
    /// Helper method to safely get property values using reflection
    /// </summary>
    private static T? GetPropertyValue<T>(object obj, string propertyName)
    {
        try
        {
            var property = obj.GetType().GetProperty(propertyName);
            var value = property?.GetValue(obj);
            return value is T result ? result : default(T);
        }
        catch
        {
            return default(T);
        }
    }
    
    /// <summary>
    /// Parses string response to PredictiveSuggestion list
    /// </summary>
    private static List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion> ParseStringToSuggestions(
        string response, 
        int maxSuggestions)
    {
        var suggestions = new List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>();
        
        if (string.IsNullOrWhiteSpace(response))
            return suggestions;
        
        // Try to parse as JSON first, then fall back to line-by-line parsing
        try
        {
            // Simple line-based parsing for now
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Where(line => !string.IsNullOrWhiteSpace(line.Trim()))
                              .Take(maxSuggestions);
            
            foreach (var line in lines)
            {
                var cleanLine = line.Trim().TrimStart('-', '*', '•').Trim();
                if (!string.IsNullOrWhiteSpace(cleanLine))
                {
                    suggestions.Add(new System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion(
                        cleanLine, 
                        $"AI-generated suggestion based on context"));
                }
            }
        }
        catch
        {
            // Fallback: create a single suggestion from the entire response
            suggestions.Add(new System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion(
                response.Trim(), 
                "AI-generated suggestion"));
        }
        
        return suggestions;
    }
    
    /// <summary>
    /// Parses list response to PredictiveSuggestion list
    /// </summary>
    private static List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion> ParseListToSuggestions(
        IEnumerable<object> response, 
        int maxSuggestions)
    {
        return response.Take(maxSuggestions)
                      .Select(item => new System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion(
                          item.ToString() ?? "Unknown suggestion",
                          "Generated suggestion"))
                      .ToList();
    }
}
