# Predictor Service

This is the background service component of the LLM Empowered Command Predictor. It runs as a standalone executable and provides command suggestions using Azure OpenAI integration with context collection.

## Overview

The Predictor Service is designed to:
- Run independently of PowerShell sessions
- Collect context information about the user's environment and command history
- Generate intelligent command suggestions using Azure OpenAI
- Demonstrate proper service hosting and lifecycle management
- Provide comprehensive logging for debugging and monitoring

## Current Implementation

This service includes the following features:
- ✅ Runs as a console application with proper hosting
- ✅ **Context Collection System**: Gathers command history, working directory, and environment info
- ✅ **Azure OpenAI Integration**: Uses Azure API Key for secure authentication
- ✅ **Prompt Template System**: Merges collected context with prompt templates
- ✅ **LLM Command Generation**: Generates 5 PowerShell command suggestions every 60 seconds
- ✅ Handles graceful shutdown with Ctrl+C
- ✅ Uses structured logging with different levels
- ✅ Demonstrates .NET BackgroundService pattern

## Architecture

```
┌─────────────────────────────────────────────┐
│              Predictor Service              │
├─────────────────────────────────────────────┤
│  Program.cs                                 │
│  ├─ Entry point and host setup              │
│  ├─ Service registration                    │
│  ├─ Azure OpenAI configuration              │
│  └─ Logging configuration                   │
├─────────────────────────────────────────────┤
│  PredictorBackgroundService                 │
│  ├─ Context collection demonstration        │
│  ├─ LLM suggestion generation               │
│  ├─ Periodic status logging                 │
│  └─ Graceful shutdown handling              │
├─────────────────────────────────────────────┤
│  Context System                             │
│  ├─ ContextManager: Coordinates providers   │
│  ├─ CommandHistoryProvider: Tracks commands │
│  └─ PredictorContext: Data structure        │
├─────────────────────────────────────────────┤
│  Azure OpenAI Integration                   │
│  ├─ AzureOpenAIService: LLM communication   │
│  ├─ PromptTemplateService: Template merging │
│  └─ ServiceConfiguration: Settings          │
└─────────────────────────────────────────────┘
```

## Features

### Context Collection
- **Command History**: Maintains recent command history with success tracking
- **Working Directory**: Tracks current directory and file contents
- **Environment Variables**: Captures relevant system environment
- **Command Frequency**: Analyzes command usage patterns
- **Execution Metadata**: Records timing and exit codes

### Azure OpenAI Integration
- **Azure API Key**: Secure authentication using API key
- **Chat Completions**: Uses GPT models for intelligent suggestions
- **Error Handling**: Robust error handling and retry logic
- **JSON Validation**: Validates LLM responses for proper format

### Prompt Engineering
- **Template System**: Uses structured prompt templates
- **Context Serialization**: Properly formats context for LLM consumption
- **Field Alignment**: Maps context fields to template expectations
- **Example-Driven**: Includes few-shot examples for better results

## Usage

### Prerequisites

1. **Azure OpenAI Service**: Deploy an Azure OpenAI service in your subscription
2. **API Key**: Get your Azure OpenAI API key from the Azure portal

### Configuration

Set the required environment variables:

```powershell
# PowerShell
$env:AZURE_OPENAI_ENDPOINT = "https://your-openai-resource.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT = "gpt-4"  # or gpt-35-turbo
$env:AZURE_OPENAI_KEY = "your-api-key-here"
```

```bash
# Linux/macOS
export AZURE_OPENAI_ENDPOINT=https://your-openai-resource.openai.azure.com/
export AZURE_OPENAI_DEPLOYMENT=gpt-4
export AZURE_OPENAI_KEY=your-api-key-here
```

### Running the Service

```bash
# Build the project
dotnet build

# Run the service
dotnet run

# Or run the executable directly
./bin/Debug/net6.0/LLMEmpoweredCommandPredictor.PredictorService.exe
```

### Service Output

When running with Azure OpenAI configured, you'll see logs like:
```
Starting LLM Empowered Command Predictor Service...
info: Azure OpenAI client initialized with endpoint: https://your-openai-resource.openai.azure.com/
info: Context Manager initialized successfully
info: Service is running... Uptime: 30 seconds
info: Demonstrating context collection and LLM integration...
info: Context collected: UserInput: 'Get-Process', WorkingDir: 'Q:\src\...', HistoryCount: 12, Session: abc123
info: Generating LLM suggestions...

=== LLM Command Suggestions ===
[
  {
    "command": ["Get-Process", "-Name", "powershell"],
    "description": "Filter processes to show only PowerShell instances. Useful for managing PowerShell sessions.",
    "confidence": 0.92
  },
  {
    "command": ["Stop-Process", "-Name", "notepad", "-Confirm"],
    "description": "Stop Notepad processes with confirmation. Safe way to close applications.",
    "confidence": 0.85
  },
  ...
]
=== End of LLM Response ===
```

### Without Azure OpenAI

If Azure OpenAI is not configured, the service still demonstrates context collection:
```
Azure OpenAI configuration not provided. Set AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_DEPLOYMENT, and AZURE_OPENAI_KEY environment variables to enable LLM features.
info: Context collected: UserInput: 'Get-Process', WorkingDir: 'Q:\src\...', HistoryCount: 12, Session: abc123
info: Azure OpenAI service not configured. Skipping LLM suggestions.
```

### Stopping the Service

Press `Ctrl+C` to gracefully stop the service.

## Configuration Options

### Azure OpenAI Settings
- `AZURE_OPENAI_ENDPOINT`: Your Azure OpenAI service endpoint URL
- `AZURE_OPENAI_DEPLOYMENT`: The deployment name (e.g., "gpt-4", "gpt-35-turbo")
- `AZURE_OPENAI_KEY`: Your Azure OpenAI API key

### Prompt Template
- Location: `src/Prompt/LLMSuggestionPromptTemplateV1.txt`
- The service automatically locates and loads this template
- Context is merged at the `{{input context placeholder}}` location

### Authentication
Uses Azure API Key authentication:
- Set the `AZURE_OPENAI_KEY` environment variable with your API key
- API key can be found in the Azure portal under your OpenAI resource
- Ensure the API key has appropriate permissions for your deployment

## Troubleshooting

### Common Issues

1. **"Azure OpenAI configuration not provided"**
   - Set all required environment variables: `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT`, and `AZURE_OPENAI_KEY`

2. **Authentication errors**
   - Verify your API key is correct and has not expired
   - Check that the API key has access to the specified deployment

3. **"Deployment not found" errors**
   - Verify the deployment name matches your Azure OpenAI service
   - Ensure the model is deployed and running

4. **"Prompt template not found"**
   - Verify the template file exists at `src/Prompt/LLMSuggestionPromptTemplateV1.txt`
   - Check file permissions

### Detailed Configuration Guide

See [`CONFIGURATION.md`](./CONFIGURATION.md) for detailed setup instructions.

## Future Enhancements

- ⏳ **Protocol Integration**: Implement ISuggestionService for IPC communication
- ⏳ **Advanced Caching**: Cache suggestions with TTL and LRU eviction
- ⏳ **Real PowerShell Integration**: Access actual PowerShell history and modules
- ⏳ **Performance Optimization**: Reduce latency and improve response times
- ⏳ **Configuration Management**: Support for appsettings.json configuration
- ⏳ **Windows Service**: Deploy as a Windows service for production
- ⏳ **Metrics and Telemetry**: Performance monitoring and usage analytics

## Development

### Project Structure

```
PredictorService/
├── Program.cs                              # Entry point and host configuration
├── CONFIGURATION.md                        # Detailed setup guide
├── Services/
│   ├── PredictorBackgroundService.cs       # Main background service
│   ├── AzureOpenAIService.cs              # Azure OpenAI client wrapper
│   └── PromptTemplateService.cs           # Template management
├── Configuration/
│   └── ServiceConfiguration.cs            # Configuration classes
├── Context/                               # Context collection system
│   ├── ContextManager.cs                 # Context coordination
│   ├── CommandHistoryContextProvider.cs  # Command history tracking
│   ├── PredictorContext.cs              # Data structures
│   └── IContextProvider.cs              # Provider interface
└── README.md                             # This file
```

### Adding New Features

1. **Add New Context Providers**: Implement `IContextProvider` for additional context sources
2. **Enhance Prompt Templates**: Modify template structure for better results
3. **Add Caching**: Implement response caching to reduce API calls
4. **Add Protocol Support**: Integrate with the Protocol project for IPC
5. **Add Configuration**: Support external configuration files
