# LLM Empowered Command Predictor - Configuration Guide

This document describes how to configure the LLM Empowered Command Predictor service to work with Azure OpenAI using API key authentication.

## Prerequisites

1. **Azure OpenAI Service**: You need an Azure OpenAI service instance deployed in your Azure subscription.
2. **API Key**: You need an API key from your Azure OpenAI service. This can be found in the Azure portal under your OpenAI resource in the "Keys and Endpoint" section.

## Configuration

The service reads configuration from environment variables:

### Required Environment Variables

```bash
# Azure OpenAI endpoint URL (replace with your actual endpoint)
AZURE_OPENAI_ENDPOINT=https://your-openai-resource.openai.azure.com/

# Deployment name of your model (e.g., gpt-4, gpt-35-turbo)
AZURE_OPENAI_DEPLOYMENT=gpt-4

# API key for authentication (replace with your actual API key)
AZURE_OPENAI_KEY=your-api-key-here
```

### Setting Environment Variables

#### Windows PowerShell
```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://your-openai-resource.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT = "gpt-4"
$env:AZURE_OPENAI_KEY = "your-api-key-here"
```

#### Windows Command Prompt
```cmd
set AZURE_OPENAI_ENDPOINT=https://your-openai-resource.openai.azure.com/
set AZURE_OPENAI_DEPLOYMENT=gpt-4
set AZURE_OPENAI_KEY=your-api-key-here
```

#### Linux/macOS
```bash
export AZURE_OPENAI_ENDPOINT=https://your-openai-resource.openai.azure.com/
export AZURE_OPENAI_DEPLOYMENT=gpt-4
export AZURE_OPENAI_KEY=your-api-key-here
```

## Authentication Setup

The service uses Azure OpenAI API key authentication. You need to:

1. **Get your API key** from the Azure portal:
   - Navigate to your Azure OpenAI resource
   - Go to "Keys and Endpoint" section
   - Copy one of the available keys

2. **Set the environment variable**:
   ```bash
   export AZURE_OPENAI_KEY=your-api-key-here
   ```

### Security Best Practices

- Store API keys securely and never commit them to source control
- Use environment variables or secure configuration management systems
- Rotate API keys regularly
- Restrict API key access to only necessary services

## Testing the Configuration

1. Set the environment variables as described above
2. Build and run the service:
   ```bash
   dotnet build
   dotnet run
   ```
3. Watch the console output for:
   - "Azure OpenAI client initialized" message
   - LLM responses every 60 seconds during context collection demonstrations

## Troubleshooting

### Common Issues

1. **"Azure OpenAI configuration not provided"**
   - Ensure all three environment variables are set: `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT`, and `AZURE_OPENAI_KEY`

2. **Authentication errors**
   - Verify your API key is correct and has not expired
   - Check that the API key has access to the specified deployment
   - Ensure the endpoint URL is correct

3. **"Deployment not found" errors**
   - Verify the deployment name matches exactly what's configured in your Azure OpenAI service
   - Check that the model is deployed and running

4. **"Prompt template not found"**
   - Ensure the `LLMSuggestionPromptTemplateV1.txt` file exists in the `src/Prompt/` directory
   - Check file permissions

### Logs

The service provides detailed logging. Look for:
- Azure OpenAI client initialization messages
- Context collection and prompt generation logs
- LLM response handling logs
- Any error messages with details

## Security Considerations

- Never hardcode API keys in source code
- Use environment variables or Azure Key Vault for sensitive configuration
- Regularly rotate API keys
- Store API keys securely and restrict access
- Monitor API usage and costs to detect unauthorized access

## Cost Management

- The service makes API calls to Azure OpenAI every 60 seconds during demonstration
- Monitor your Azure OpenAI usage and costs
- Consider adjusting the demonstration frequency in production environments
- Use appropriate model sizes for your use case (e.g., GPT-3.5-turbo vs GPT-4)
