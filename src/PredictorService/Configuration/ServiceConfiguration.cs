namespace LLMEmpoweredCommandPredictor.PredictorService.Configuration;

/// <summary>
/// Configuration settings for Azure OpenAI integration.
/// </summary>
public class AzureOpenAIConfiguration
{
    /// <summary>
    /// Azure OpenAI endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Deployment name for the model to use.
    /// </summary>
    public string DeploymentName { get; set; } = string.Empty;
    
    /// <summary>
    /// Azure OpenAI Api Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Validates that the configuration is complete and valid.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Endpoint) &&
               !string.IsNullOrWhiteSpace(DeploymentName) &&
               !string.IsNullOrWhiteSpace(ApiKey) &&
               Uri.TryCreate(Endpoint, UriKind.Absolute, out _);
    }
}

/// <summary>
/// Configuration settings for the prompt template.
/// </summary>
public class PromptConfiguration
{
    /// <summary>
    /// Path to the prompt template file.
    /// </summary>
    public string TemplatePath { get; set; } = string.Empty;

    /// <summary>
    /// Validates that the configuration is complete and valid.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(TemplatePath) && File.Exists(TemplatePath);
    }
}
