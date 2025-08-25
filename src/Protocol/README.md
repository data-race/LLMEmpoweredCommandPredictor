# IPC Protocol Layer

This directory contains the IPC (Inter-Process Communication) protocol layer that enables communication between a client and server using Microsoft's StreamJsonRpc library over named pipes.

## Overview

The protocol layer provides a type-safe communication infrastructure with the following key characteristics:

- **Contract-First Design**: All communication is defined through strict C# interfaces
- **Fast Response Times**: Optimized for sub-20ms response times
- **Graceful Degradation**: Returns empty results if service is unavailable
- **Robust Error Handling**: Never crashes the client process
- **Background Processing**: Supports asynchronous operations

## Architecture

The protocol follows a classic client-server model:

```
┌─────────────────┐    IPC Protocol    ┌─────────────────┐
│     Client      │ ◄────────────────► │     Server      │
│                 │                    │                 │
│ - Fast response │                    │ - Business logic│
│ - Connection mgmt│                   │ - Data storage  │
│ - Error handling│                    │ - Background ops│
└─────────────────┘                    └─────────────────┘
```

## Core Components

### 1. Contracts (`Contracts/`)

**`ISuggestionService.cs`** - Core interface defining all RPC methods:

```csharp
public interface ISuggestionService
{
    Task<SuggestionResponse> GetSuggestionsAsync(SuggestionRequest request, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
    Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task TriggerCacheRefreshAsync(SuggestionRequest request, CancellationToken cancellationToken = default);
    Task ClearCacheAsync(CancellationToken cancellationToken = default);
}
```

### 2. Models (`Models/`)

**`SuggestionRequest.cs`** - Request data structure:
```csharp
public class SuggestionRequest
{
    public required string UserInput { get; init; }
    public string WorkingDirectory { get; init; } = string.Empty;
    public int MaxSuggestions { get; init; } = 5;
}
```

**`SuggestionResponse.cs`** - Response data structure:
```csharp
public class SuggestionResponse
{
    public IReadOnlyList<PredictiveSuggestion> Suggestions { get; init; } = new List<PredictiveSuggestion>();
    public string Source { get; init; } = "unknown";
    public double ConfidenceScore { get; init; } = 1.0;
    public string? WarningMessage { get; init; }
}
```

**`ServiceStatus.cs`** - Service health information:
```csharp
public class ServiceStatus
{
    public bool IsRunning { get; init; }
    public TimeSpan Uptime { get; init; }
}
```

**`ConnectionSettings.cs`** - Configuration options:
```csharp
public class ConnectionSettings
{
    public int TimeoutMs { get; init; } = 15;
    public int ConnectionTimeoutMs { get; init; } = 1000;
    public int MaxRetries { get; init; } = 3;
    public int RetryDelayMs { get; init; } = 100;
    public bool EnableDebugLogging { get; init; } = false;
}
```

**`ServiceException.cs`** - Custom exception for service errors:
```csharp
public class ServiceException : Exception
{
    public ServiceException(string message) : base(message) { }
    public ServiceException(string message, Exception innerException) : base(message, innerException) { }
}
```

### 3. Client (`Client/`)

**`SuggestionServiceClient.cs`** - Simple client implementation with basic connection management:
- **Connection Management**: Basic connection establishment and cleanup
- **Timeout Handling**: Ensures 20ms response time constraint
- **Error Handling**: Graceful degradation with empty responses
- **Minimal Overhead**: Focused on essential functionality only

### 4. Server (`Server/`)

**`SuggestionServiceServer.cs`** - Simple server implementation for handling client connections:
- **Single Connection**: Handles one client at a time (sufficient for hackathon)
- **Resource Management**: Proper cleanup of connections and resources
- **Error Handling**: Basic error handling with automatic recovery
- **Minimal Overhead**: Focused on essential functionality only

### 5. Factory (`Factory/`)

**`ProtocolFactory.cs`** - Factory methods for creating client and server instances:

```csharp
public static class ProtocolFactory
{
    public static SuggestionServiceClient CreateClient();
    public static SuggestionServiceClient CreateClient(ConnectionSettings settings);
    public static SuggestionServiceServer CreateServer(ISuggestionService service);
    public static ConnectionSettings DefaultSettings { get; }
}
```

## Usage Examples

### Basic Client Usage

```csharp
// Create client with default settings
using var client = ProtocolFactory.CreateClient();

// Create request
var request = new SuggestionRequest
{
    UserInput = "git",
    WorkingDirectory = Environment.CurrentDirectory,
    MaxSuggestions = 5
};

// Get suggestions (returns within 20ms)
var response = await client.GetSuggestionsAsync(request);
var suggestions = response.Suggestions;
```

### Basic Server Usage

```csharp
// Create service implementation
var service = new MySuggestionService();

// Create and start server
using var server = ProtocolFactory.CreateServer(service);
await server.StartAsync();

// Server runs until cancelled
await Task.Delay(-1, cancellationToken);
```

### Custom Configuration

```csharp
// Create custom settings
var settings = new ConnectionSettings
{
    TimeoutMs = 15,
    MaxRetries = 3,
    EnableDebugLogging = true
};

// Create client with custom settings
using var client = new SuggestionServiceClient(settings);
```

## Performance Characteristics

### Response Time Guarantees

- **GetSuggestionsAsync**: Returns within 20ms
- **Cache Hits**: Return immediately with cached data
- **Cache Misses**: Return empty results immediately
- **Connection Failures**: Return empty results immediately

### Resource Management

- **Basic Connections**: Simple connection model without pooling
- **Automatic Cleanup**: Proper disposal of resources and connections
- **Memory Efficiency**: Minimal memory footprint for client operations

## Error Handling

### Client-Side Error Handling

- **Service Unavailable**: Returns empty results, doesn't crash client
- **Connection Timeouts**: Returns empty results immediately
- **Network Errors**: Graceful degradation with empty responses
- **Invalid Responses**: Fallback to empty results

### Server-Side Error Handling

- **Client Disconnections**: Clean connection cleanup
- **Service Failures**: Basic error handling with automatic recovery
- **Resource Management**: Simple resource cleanup without complex tracking

## Configuration Options

### Connection Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `TimeoutMs` | 15ms | RPC call timeout (must be < 20ms) |
| `ConnectionTimeoutMs` | 1000ms | Connection establishment timeout |
| `MaxRetries` | 3 | Maximum retry attempts |
| `RetryDelayMs` | 100ms | Delay between retries |
| `EnableDebugLogging` | false | Enable detailed logging |

### Pre-configured Settings

- `ProtocolFactory.DefaultSettings`: Optimized for production use

## Best Practices

### Client Development

1. **Always use async/await**: Never block the UI thread
2. **Handle all exceptions**: Return empty results instead of throwing
3. **Monitor connection state**: Use events for debugging and monitoring
4. **Use appropriate timeouts**: Respect the 20ms constraint
5. **Dispose resources**: Use `using` statements for proper cleanup

### Server Development

1. **Implement fast operations**: Return cached results immediately
2. **Use background processing**: Never block on slow operations
3. **Handle client disconnections**: Clean up resources properly
4. **Monitor performance**: Track response times and throughput
5. **Implement health checks**: Provide status information for monitoring

### Error Handling

1. **Graceful degradation**: Always return valid results, even if empty
2. **Log errors appropriately**: Use debug logging for troubleshooting
3. **Retry transient failures**: Implement retry logic for network issues
4. **Monitor error rates**: Track and alert on high error rates

## Dependencies

- **StreamJsonRpc**: Microsoft's lightweight RPC library for IPC communication
- **Microsoft.VisualStudio.Threading**: Async utilities and threading helpers

## License

This project is part of the Microsoft 2025 Global Annual Hackathon submission.
