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

## Test Files

### FinalTest.ps1
Main test script that enables PowerShell experimental features, builds projects, and loads the plugin module.

Run: `pwsh -File FinalTest.ps1`

### VerifyCache.ps1  
Cache verification script that provides instructions for testing cache hit/miss behavior.

Run: `pwsh -File VerifyCache.ps1`