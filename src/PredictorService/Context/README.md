# Context Collection System

This directory contains the context collection infrastructure for the LLM Empowered Command Predictor. The context system gathers relevant information about the user's environment and command history to provide better suggestions.

## Overview

The context system is designed with a provider pattern that allows for extensible and modular context collection:

```
┌─────────────────────────────────────┐
│          Context Manager            │
│  ┌─────────────────────────────────┐ │
│  │     IContextProvider            │ │
│  │  ┌─────────────────────────────┐ │ │
│  │  │ CommandHistoryProvider      │ │ │
│  │  └─────────────────────────────┘ │ │
│  │  ┌─────────────────────────────┐ │ │
│  │  │ ModuleInfoProvider (future) │ │ │
│  │  └─────────────────────────────┘ │ │
│  │  ┌─────────────────────────────┐ │ │
│  │  │ EnvironmentProvider (future)│ │ │
│  │  └─────────────────────────────┘ │ │
│  └─────────────────────────────────┘ │
└─────────────────────────────────────┘
```

## Components

### Core Data Classes

#### `PredictorContext.cs`
- **PredictorContext**: Main data structure holding all context information
- **CommandHistoryEntry**: Represents a single command execution record

```csharp
public class PredictorContext
{
    public string UserInput { get; set; }
    public string WorkingDirectory { get; set; }
    public IReadOnlyList<CommandHistoryEntry> CommandHistory { get; set; }
    public DateTime Timestamp { get; set; }
    public string SessionId { get; set; }
    // ... other properties
}
```

### Interface

#### `IContextProvider.cs`
Defines the contract for context providers:
- `CollectContextAsync()` - Gathers context information
- `UpdateContextAsync()` - Updates context after command execution
- `InitializeAsync()` - Initializes the provider
- `DisposeAsync()` - Cleans up resources

### Implementations

#### `CommandHistoryContextProvider.cs`
Collects and manages command history information:
- ✅ Maintains recent command history (up to 1000 entries)
- ✅ Tracks command success/failure
- ✅ Records execution timestamps and working directories
- ✅ Provides simulated initial history for testing
- ✅ Thread-safe operations with locking

#### `ContextManager.cs`
Coordinates multiple context providers:
- ✅ Registers and manages multiple providers
- ✅ Prioritizes providers by importance
- ✅ Merges context from multiple sources
- ✅ Handles provider failures gracefully
- ✅ Provides unified context collection interface

## Usage

### Basic Context Collection

```csharp
// Initialize context manager
var contextManager = new ContextManager(logger);
contextManager.RegisterProvider(new CommandHistoryContextProvider(logger));
await contextManager.InitializeAsync();

// Collect context
var context = await contextManager.CollectContextAsync("Get-Process");

// Update context after command execution
await contextManager.UpdateContextAsync(context, "Get-Process -Name powershell", true);
```

### Integration with Background Service

The context system is automatically integrated into the background service:

1. **Initialization**: Context providers are registered and initialized on service startup
2. **Demonstration**: Every 60 seconds, the service demonstrates context collection
3. **Cleanup**: Providers are properly disposed when the service shuts down

## Current Features

### Command History Provider
- ✅ **Simulated History**: Loads initial command history for testing
- ✅ **Real-time Updates**: Tracks new command executions
- ✅ **Success Tracking**: Records whether commands succeeded
- ✅ **Metadata**: Captures working directory, timestamps, duration
- ✅ **Memory Management**: Limits history size to prevent memory issues
- ✅ **Thread Safety**: Safe for concurrent access

### Context Manager
- ✅ **Provider Registration**: Dynamic provider registration
- ✅ **Priority Handling**: Providers sorted by priority
- ✅ **Error Resilience**: Graceful handling of provider failures
- ✅ **Resource Management**: Proper initialization and cleanup
- ✅ **Logging**: Comprehensive logging for debugging

## Future Enhancements

### Additional Providers
- ⏳ **ModuleInfoProvider**: Track loaded PowerShell modules
- ⏳ **EnvironmentProvider**: Capture environment variables and system info
- ⏳ **FileSystemProvider**: Monitor file system changes in working directory
- ⏳ **AliasProvider**: Track user-defined aliases and functions

### Enhanced Features
- ⏳ **Persistence**: Save/load context data across sessions
- ⏳ **Filtering**: Smart filtering of relevant context
- ⏳ **Compression**: Compress old context data
- ⏳ **Analytics**: Context usage analytics and optimization

### Real PowerShell Integration
- ⏳ **PowerShell History**: Read actual PowerShell command history
- ⏳ **Module Detection**: Query loaded modules via PowerShell APIs
- ⏳ **Execution Context**: Capture actual execution results and timing

## Testing

The context system includes comprehensive logging and can be tested by:

1. **Running the service**: Context collection is demonstrated every 60 seconds
2. **Monitoring logs**: All context operations are logged with details
3. **Manual testing**: Context can be collected programmatically for testing

## Configuration

Current configuration options:
- **Max History Size**: 1000 entries (configurable in CommandHistoryProvider)
- **Provider Priority**: 100 for CommandHistoryProvider (higher = more important)
- **Update Frequency**: Real-time updates as commands are executed

## Performance Considerations

- **Async Operations**: All context collection is asynchronous
- **Thread Safety**: Concurrent access is properly handled
- **Memory Management**: History size is limited and trimmed automatically
- **Error Handling**: Provider failures don't affect the overall system
- **Minimal Overhead**: Context collection is designed to be fast and lightweight
