using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LLMEmpoweredCommandPredictor;

/// <summary>
/// Settings for the LLM-powered command predictor.
/// </summary>
public class LLMSuggestionSettings
{
    /// <summary>
    /// LLM API configuration settings.
    /// </summary>
    public LlmApiSettings LlmApi { get; set; } = new LlmApiSettings();

    /// <summary>
    /// Suggestion configuration settings.
    /// </summary>
    public SuggestionSettings Suggestions { get; set; } = new SuggestionSettings();

    /// <summary>
    /// The prompt template used for generating suggestions.
    /// </summary>
    public string PromptTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Loads settings from the specified YAML file.
    /// </summary>
    /// <param name="filePath">Path to the settings YAML file. If null, uses the default path.</param>
    /// <returns>The loaded settings.</returns>
    public static LLMSuggestionSettings LoadFromFile(string filePath = "settings.yaml")
    {   
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Warning: Settings file not found at {filePath}. Using default settings.");
            return new LLMSuggestionSettings();
        }
        
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yaml = File.ReadAllText(filePath);
            var settings = deserializer.Deserialize<LLMSuggestionSettings>(yaml);

            // Load prompt from Markdown file
            var promptFilePath = "prompt.md";
            if (File.Exists(promptFilePath))
            {
                settings.PromptTemplate = File.ReadAllText(promptFilePath);
            }

            return settings;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
            return new LLMSuggestionSettings();
        }
    }
}

/// <summary>
/// LLM API configuration settings.
/// </summary>
public class LlmApiSettings
{
    /// <summary>
    /// The endpoint URL for the LLM API.
    /// </summary>
    public string Endpoint { get; set; } = "https://api.example.com/v1/completions";

    /// <summary>
    /// The secret API key for authentication.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;
}

/// <summary>
/// Configuration settings for suggestions.
/// </summary>
public class SuggestionSettings
{
    /// <summary>
    /// Maximum number of suggestions to produce.
    /// </summary>
    public int MaxCount { get; set; } = 5;

    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    public int Timeout { get; set; } = 3000;
}

