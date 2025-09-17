using LLMEmpoweredCommandPredictor.PredictorService.Context;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMEmpoweredCommandPredictor.PredictorService.Services;

/// <summary>
/// Service for managing prompt templates and merging context data.
/// </summary>
public class PromptTemplateService
{
    private readonly ILogger<PromptTemplateService> _logger;
    private readonly string _templatePath;
    private string? _cachedTemplate;

    public PromptTemplateService(ILogger<PromptTemplateService> logger, string templatePath)
    {
        _logger = logger;
        _templatePath = templatePath;
    }

    /// <summary>
    /// Loads the prompt template from file.
    /// </summary>
    /// <returns>The prompt template content</returns>
    private async Task<string> LoadTemplateAsync()
    {
        if (_cachedTemplate != null)
        {
            return _cachedTemplate;
        }

        try
        {
            if (!File.Exists(_templatePath))
            {
                throw new FileNotFoundException($"Prompt template file not found: {_templatePath}");
            }

            _cachedTemplate = await File.ReadAllTextAsync(_templatePath);
            _logger.LogInformation("Prompt template loaded from: {TemplatePath}", _templatePath);
            return _cachedTemplate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load prompt template from: {TemplatePath}", _templatePath);
            throw;
        }
    }

    /// <summary>
    /// Merges the context with the prompt template by replacing the placeholder.
    /// </summary>
    /// <param name="context">The context to merge into the template</param>
    /// <returns>The complete prompt with context merged in</returns>
    public async Task<string> MergeContextWithTemplateAsync(PredictorContext context)
    {
        try
        {
            var template = await LoadTemplateAsync();
            var contextJson = SerializeContextForPrompt(context);

            // Replace the placeholder with the serialized context
            var mergedPrompt = template.Replace("{{input context placeholder}}", contextJson);

            _logger.LogDebug("Context merged with template. Context length: {ContextLength} characters", contextJson.Length);

            return mergedPrompt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge context with template");
            throw;
        }
    }

    /// <summary>
    /// Serializes the context object for use in the prompt template.
    /// Maps the PredictorContext to the expected format in the template.
    /// </summary>
    /// <param name="context">The context to serialize</param>
    /// <returns>JSON representation of the context formatted for the prompt</returns>
    private string SerializeContextForPrompt(PredictorContext context)
    {
        try
        {
            // Transform the context into the format expected by the prompt template
            var promptContext = new PromptContextData
            {
                GlobalCommandsHistory = context.CommandHistory
                    .Take(300)
                    .Select(h => h.Command)
                    .ToList(), 
                PreviousCommands = context.SessionHistory
                    .Take(5) // Get last 5 commands
                    .Select(h => h.Command)
                    .ToList(),
                CurrentDirectory = context.WorkingDirectory,
                DirectoryContents = GetDirectoryContents(context.WorkingDirectory),
                InstalledModules = new List<string>(), // No data available in PredictorContext
                LastCommandOutput = context.CommandHistory.Count > 0 
                    ? (context.CommandHistory.First().IsSuccessful ? "Command executed successfully" : "Command failed with error")
                    : string.Empty,
                EnvironmentVariables = new Dictionary<string, string>(), // No data available in PredictorContext
                CommandExecutionTime = context.CommandHistory.Count > 0 && context.CommandHistory.First().Duration.HasValue
                    ? $"{context.CommandHistory.First().Duration!.Value.TotalSeconds:F1}s"
                    : string.Empty,
                CommandFrequency = context.CommandHistory
                    .GroupBy(h => h.Command.Split(' ').FirstOrDefault() ?? h.Command)
                    .ToDictionary(g => g.Key, g => g.Count()),
                CommandParametersUsed = context.CommandHistory
                    .Take(3)
                    .SelectMany(h => h.Command.Split(' ').Where(part => part.StartsWith('-')))
                    .Distinct()
                    .ToList(),
                SessionDuration = !string.IsNullOrEmpty(context.SessionId) 
                    ? $"{(DateTime.UtcNow - context.Timestamp).TotalMinutes:F0}m"
                    : string.Empty,
                ClipboardContents = string.Empty // No data available in PredictorContext
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            _logger.LogInformation("using previous commands: {0}", promptContext.PreviousCommands);
            return JsonSerializer.Serialize(promptContext, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize context for prompt");
            throw;
        }
    }

    /// <summary>
    /// Gets the contents of the specified directory.
    /// </summary>
    private List<string> GetDirectoryContents(string directory)
    {
        try
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return new List<string>();
            }

            return Directory.GetFileSystemEntries(directory)
                .Take(20) // Limit to avoid overwhelming the prompt
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList()!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get directory contents for: {Directory}", directory);
            return new List<string>();
        }
    }
}

/// <summary>
/// Data structure that matches the expected format in the prompt template.
/// </summary>
public class PromptContextData
{
    public List<string> GlobalCommandsHistory { get; set; } = new();
    public List<string> PreviousCommands { get; set; } = new();
    public string CurrentDirectory { get; set; } = string.Empty;
    public List<string> DirectoryContents { get; set; } = new();
    public List<string> InstalledModules { get; set; } = new();
    public string LastCommandOutput { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public string CommandExecutionTime { get; set; } = string.Empty;
    public Dictionary<string, int> CommandFrequency { get; set; } = new();
    public List<string> CommandParametersUsed { get; set; } = new();
    public string SessionDuration { get; set; } = string.Empty;
    public string ClipboardContents { get; set; } = string.Empty;
}
