# Predictor Service

This is the background service component of the LLM Empowered Command Predictor. It runs as a standalone executable and will provide command suggestions through IPC communication in the future.

## Overview

The Predictor Service is designed to:
- Run independently of PowerShell sessions
- Serve as a foundation for future LLM integration
- Demonstrate service hosting and lifecycle management
- Provide a simple logging example

## Current Implementation

This is currently an **empty service** that serves as a foundation for future development. It:
- ✅ Runs as a console application with proper hosting
- ✅ Outputs "Hello World" message on startup
- ✅ Logs periodic status messages every 30 seconds
- ✅ Handles graceful shutdown with Ctrl+C
- ✅ Uses structured logging with different levels
- ✅ Demonstrates .NET BackgroundService pattern

## Architecture

```
┌─────────────────────────────────────┐
│         Predictor Service           │
├─────────────────────────────────────┤
│  Program.cs                         │
│  ├─ Entry point and host setup      │
│  ├─ Service registration            │
│  └─ Logging configuration           │
├─────────────────────────────────────┤
│  PredictorBackgroundService         │
│  ├─ Implements BackgroundService    │
│  ├─ Simple run loop with counter    │
│  ├─ Periodic status logging         │
│  └─ Graceful shutdown handling      │
└─────────────────────────────────────┘
```

## Usage

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

When running, you'll see logs like:
```
Starting LLM Empowered Command Predictor Service...
info: LLMEmpoweredCommandPredictor.PredictorService.Services.PredictorBackgroundService[0]
      Predictor Background Service starting...
info: LLMEmpoweredCommandPredictor.PredictorService.Services.PredictorBackgroundService[0]
      Hello World from LLM Empowered Command Predictor Service!
info: LLMEmpoweredCommandPredictor.PredictorService.Services.PredictorBackgroundService[0]
      Service is running... Uptime: 30 seconds
info: LLMEmpoweredCommandPredictor.PredictorService.Services.PredictorBackgroundService[0]
      Service is running... Uptime: 60 seconds
```

### Stopping the Service

Press `Ctrl+C` to gracefully stop the service. You'll see:
```
info: LLMEmpoweredCommandPredictor.PredictorService.Services.PredictorBackgroundService[0]
      Predictor Background Service shutdown requested
info: LLMEmpoweredCommandPredictor.PredictorService.Services.PredictorBackgroundService[0]
      Predictor Background Service stop requested
info: LLMEmpoweredCommandPredictor.PredictorService.Services.PredictorBackgroundService[0]
      Predictor Background Service stopping...
```

## Future Implementation

This empty service will be expanded to include:

- ⏳ Protocol integration (ISuggestionService implementation)
- ⏳ Named pipe server for IPC communication
- ⏳ Integration with LLM APIs (OpenAI, Azure OpenAI, etc.)
- ⏳ Advanced caching with TTL and LRU eviction
- ⏳ Context collection (command history, modules, etc.)
- ⏳ Prompt engineering and optimization
- ⏳ Background cache warming
- ⏳ Performance metrics and telemetry
- ⏳ Configuration management
- ⏳ Windows Service deployment

## Development

### Adding New Features

To expand this service:

1. **Add Protocol Support**: Reference the Protocol project and implement ISuggestionService
2. **Add IPC Communication**: Integrate SuggestionServiceServer for named pipe communication
3. **Add LLM Integration**: Implement actual AI-powered suggestion generation
4. **Add Caching**: Implement sophisticated caching mechanisms
5. **Add Configuration**: Add appsettings.json for service configuration

### Project Structure

```
PredictorService/
├── Program.cs                          # Entry point and host configuration
├── Services/
│   └── PredictorBackgroundService.cs   # Main background service implementation
├── README.md                           # This file
└── LLMEmpoweredCommandPredictor.PredictorService.csproj
```
