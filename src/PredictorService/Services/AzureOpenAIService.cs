using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using OpenAI.Chat;

namespace LLMEmpoweredCommandPredictor.PredictorService.Services;

/// <summary>
/// Service for interacting with Azure OpenAI to generate command suggestions.
/// Uses Azure API Key for authentication.
/// </summary>
public class AzureOpenAIService
{
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly AzureOpenAIClient _azureClient;
    private readonly ChatClient _chatClient;

    public AzureOpenAIService(ILogger<AzureOpenAIService> logger, string endpoint, string deploymentName, string apiKey)
    {
        _logger = logger;

        try
        {
            // Use AzureKeyCredential for API key authentication
            var credential = new AzureKeyCredential(apiKey);
            _azureClient = new AzureOpenAIClient(new Uri(endpoint), credential);
            
            // Initialize the ChatClient with the specified deployment name
            _chatClient = _azureClient.GetChatClient(deploymentName);
            
            _logger.LogInformation("Azure OpenAI client initialized with endpoint: {Endpoint} and deployment: {DeploymentName}", endpoint, deploymentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure OpenAI client");
            throw;
        }
    }

    /// <summary>
    /// Generates command suggestions using Azure OpenAI based on the provided prompt.
    /// </summary>
    /// <param name="prompt">The complete prompt with context merged in</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The response from Azure OpenAI</returns>
    public async Task<string> GenerateCommandSuggestionsAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Sending request to Azure OpenAI");

            // Create a list of chat messages
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a helpful PowerShell assistant that provides command suggestions in JSON format."),
                new UserChatMessage(prompt)
            };

            // Create chat completion options
            var options = new ChatCompletionOptions
            {
                Temperature = 0.01f, // Lower temperature for more focused suggestions
                MaxTokens = 1000,
                TopP = 0.95f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f
            };

            // Create the chat completion request
            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);

            if (completion?.Content?.Count > 0)
            {
                var content = completion.Content[0].Text;
                _logger.LogDebug("Received response from Azure OpenAI: {ContentLength} characters", content?.Length ?? 0);
                return content ?? string.Empty;
            }
            else
            {
                _logger.LogWarning("Azure OpenAI returned no content in response");
                return string.Empty;
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure OpenAI request failed with status {Status}: {Message}", ex.Status, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Azure OpenAI");
            throw;
        }
    }

    /// <summary>
    /// Validates that the response from Azure OpenAI is valid JSON.
    /// </summary>
    /// <param name="response">The response to validate</param>
    /// <returns>True if the response is valid JSON, false otherwise</returns>
    public bool IsValidJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        try
        {
            JsonDocument.Parse(response);
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI response is not valid JSON: {Response}", response);
            return false;
        }
    }
}
