# LLM Empowered Command Predictor - Protocol Layer

This is the core IPC (Inter-Process Communication) protocol layer for the LLM-powered command predictor system. It provides a fast, and extensible foundation for communication between the PowerShell predictor plugin (client) and the background service (server).

## Overview

The Protocol layer implements a **client-server architecture** using **StreamJsonRpc** over **Named Pipes** for fast, local IPC communication. It's designed with the following principles:

- **Contract-First**: All communication is defined through C# interfaces
- **Performance**: Optimized for sub-20ms response times
- **Reliability**: Robust error handling and connection management
- **Extensibility**: Rich configuration options and monitoring capabilities

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Protocol Layer                              │
├─────────────────────────────────────────────────────────────────┤
│  Contracts/           Models/           Factory/              │
│  ├─ ISuggestionService  ├─ SuggestionRequest   ├─ ProtocolFactory │
│  └─ (RPC Interface)     ├─ SuggestionResponse  └─ (Client/Server │
│                         ├─ ServiceStatus         Creation)       │
│                         ├─ ConnectionSettings                    │
│                         └─ ServiceException                      │
├─────────────────────────────────────────────────────────────────┤
│  Client/               Server/                                 │
│  ├─ SuggestionServiceClient  ├─ SuggestionServiceServer        │
│  └─ (RPC Client)             └─ (RPC Server)                   │
└─────────────────────────────────────────────────────────────────┘
```

## Key Components

### 1. Contracts (`ISuggestionService`)

The core RPC interface defining all available methods:

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

### 2. Models

#### SuggestionRequest
Rich context information for generating suggestions:

```csharp
public class SuggestionRequest
{
    public string UserInput { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public int MaxSuggestions { get; set; } = 5;
    public IReadOnlyList<string> CommandHistory { get; set; } = new List<string>();
    public string PowerShellVersion { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string UserSessionId { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
    public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;
}
```

#### SuggestionResponse
Comprehensive suggestion results with metadata:

```csharp
public class SuggestionResponse
{
    public IReadOnlyList<PredictiveSuggestion> Suggestions { get; set; } = new List<PredictiveSuggestion>();
    public string Source { get; set; } = "unknown";
    public double ConfidenceScore { get; set; } = 1.0;
    public string? WarningMessage { get; set; }
    public DateTime GeneratedTimestamp { get; set; } = DateTime.UtcNow;
    public DateTime? CachedTimestamp { get; set; }
    public bool IsFromCache { get; set; } = false;
    public double GenerationTimeMs { get; set; } = 0.0;
    public double CacheHitRate { get; set; } = 0.0;
    public string? RequestCorrelationId { get; set; }
    public DateTime ServerCreatedTimestamp { get; set; } = DateTime.UtcNow;
}
```

#### ServiceStatus
Detailed service health and performance metrics:

```csharp
public class ServiceStatus
{
    public bool IsRunning { get; set; } = true;
    public TimeSpan Uptime { get; set; } = TimeSpan.Zero;
    public int CachedSuggestionsCount { get; set; } = 0;
    public long TotalRequestsProcessed { get; set; } = 0;
    public double AverageResponseTimeMs { get; set; } = 0.0;
    public DateTimeOffset LastCacheUpdate { get; set; } = DateTimeOffset.UtcNow;
    public string Version { get; set; } = "1.0.0";
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StatusTimestamp { get; set; } = DateTimeOffset.UtcNow;
    public double HealthScore { get; set; } = 1.0;
    public double MemoryUsageMb { get; set; } = 0.0;
    public double CpuUsagePercent { get; set; } = 0.0;
    public int ActiveConnections { get; set; } = 0;
}
```

#### ConnectionSettings
Advanced connection configuration options:

```csharp
public class ConnectionSettings
{
    public int TimeoutMs { get; set; } = 15;
    public int ConnectionTimeoutMs { get; set; } = 1000;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 100;
    public bool EnableConnectionPooling { get; set; } = false;
    public int MaxPoolSize { get; set; } = 5;
    public bool EnableAutoReconnect { get; set; } = false;
    public bool EnableDebugLogging { get; set; } = false;
}
```

#### ServiceException
Rich error information with categorization:

```csharp
public class ServiceException : Exception
{
    public ServiceErrorType ErrorType { get; }
    public bool IsRecoverable { get; }
    public DateTime ErrorTimestamp { get; }
    public string? ErrorContext { get; }
    public string? ErrorCode { get; }
}

public enum ServiceErrorType
{
    Unknown, Connection, Authentication, ServiceUnavailable,
    InvalidRequest, InternalError, RateLimited, Timeout, NotFound
}
```

### 3. Factory (`ProtocolFactory`)

Pre-configured client and server creation with optimized presets:

```csharp
// Different configuration presets
var highPerfClient = ProtocolFactory.CreateHighPerformanceClient();
var devClient = ProtocolFactory.CreateDevelopmentClient();
var reliableClient = ProtocolFactory.CreateReliableClient();
var debugClient = ProtocolFactory.CreateDebugClient();

// Custom configuration
var customClient = ProtocolFactory.CreateClient(new ConnectionSettings
{
    EnableConnectionPooling = true,
    MaxPoolSize = 10,
    EnableAutoReconnect = true
});
```

**Available Presets:**
- **DefaultSettings**: Balanced performance and reliability
- **HighPerformanceSettings**: Optimized for speed with connection pooling
- **DevelopmentSettings**: Detailed logging with relaxed timeouts
- **ReliableSettings**: High retry counts for critical operations

### 4. Client (`SuggestionServiceClient`)

Robust RPC client with advanced features:

```csharp
var client = new SuggestionServiceClient(settings);

// Get suggestions with rich context
var request = new SuggestionRequest
{
    UserInput = "get-proc",
    WorkingDirectory = Environment.CurrentDirectory,
    MaxSuggestions = 5,
    CommandHistory = recentCommands,
    PowerShellVersion = "7.0+",
    OperatingSystem = Environment.OSVersion.ToString(),
    UserSessionId = "user123",
    Priority = 2
};

var response = await client.GetSuggestionsAsync(request);
```

**Features:**
- Automatic connection management
- Configurable retry logic
- Connection pooling support
- Auto-reconnect capabilities
- Comprehensive error handling

### 5. Server (`SuggestionServiceServer`)

Named pipe server for hosting the suggestion service:

```csharp
var service = new MySuggestionService();
var server = new SuggestionServiceServer(service, "MyPipeName");

await server.StartAsync();
// Server now listens for client connections
```

## StreamJsonRpc Integration

The Protocol layer uses **StreamJsonRpc** for efficient RPC communication:

- **Serialization**: Automatic JSON serialization/deserialization
- **Transport**: Named pipes for fast local communication
- **Protocol**: JSON-RPC 2.0 specification
- **Performance**: Optimized for low-latency operations

## Message Serialization

All models are designed for efficient serialization:

- **JSON-friendly**: Simple properties that serialize cleanly
- **Nullable support**: Proper handling of optional values
- **DateTime handling**: ISO 8601 format for timestamps
- **Collection support**: Efficient handling of lists and arrays

## Performance Characteristics

- **Response Time**: < 20ms for cached suggestions
- **Connection Time**: < 1000ms for initial connection
- **Throughput**: High concurrent request handling
- **Memory**: Efficient object pooling and reuse