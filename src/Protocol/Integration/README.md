# Protocol Integration Layer

Bridge components that connect Protocol with PredictorPlugin without modifying existing code.

## Components

### PluginHelper (`Integration/PluginHelper.cs`)
Provides IPC communication for Plugin. Includes synchronous/asynchronous API, automatic fallback when IPC fails, and respects 20ms timeout constraint.

## Architecture

The Protocol layer now follows a clean architecture:
```
Plugin -> IPC -> PredictorServiceBackend -> Cache
```

PredictorServiceBackend directly implements ISuggestionService and manages its own caching logic.

## Testing

### TestConnection.ps1
Connection test script that loads the PowerShell module and enables predictions.

Run: `pwsh -File TestConnection.ps1`
