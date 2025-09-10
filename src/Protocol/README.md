# Protocol Layer

IPC protocol layer for communication between PowerShell predictor plugin and background service using StreamJsonRpc over Named Pipes.

## Components

- **Contracts**: ISuggestionService interface
- **Models**: Request/Response data structures  
- **Client**: SuggestionServiceClient
- **Server**: SuggestionServiceServer
- **Factory**: ProtocolFactory for creating clients and servers
- **Integration**: Bridge components for connecting with existing code

## Usage

### Basic Client
```csharp
var client = ProtocolFactory.CreateClient();
var request = new SuggestionRequest { UserInput = "git" };
var response = await client.GetSuggestionsAsync(request);
```

### Basic Server
```csharp
var service = new MySuggestionService();
var server = ProtocolFactory.CreateServer(service);
await server.StartAsync();
```

### Cached Server
```csharp
var backend = new MyServiceBackend();
var cache = new MyCacheService();
var keyGenerator = new MyCacheKeyGenerator();
var server = ProtocolFactory.CreateCachedServer(backend, cache, keyGenerator);
await server.StartAsync();
```

## Testing

### TestConnection.ps1
Connection test script that loads the PowerShell module and enables predictions.

Run: `pwsh -File TestConnection.ps1`