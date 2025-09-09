# Protocol Integration Layer

Bridge components that connect Protocol with PredictorPlugin and PredictorService without modifying existing code.

## Components

### ContextTransformer (`Adapters/ContextTransformer.cs`)
Transforms data between different context models using reflection. Handles Plugin context to Protocol request, Protocol request to Service context, and Service response to Protocol response.

### ServiceBridge (`Integration/ServiceBridge.cs`) 
Implements ISuggestionService interface by bridging to existing PredictorService logic. Uses IServiceBackend interface to avoid coupling.

### PluginHelper (`Integration/PluginHelper.cs`)
Provides IPC communication for Plugin. Includes synchronous/asynchronous API, automatic fallback when IPC fails, and respects 20ms timeout constraint.

### CachedServiceBridge (`Integration/CachedServiceBridge.cs`)
Adds caching layer to any ISuggestionService implementation. Handles cache key generation, TTL management, and cache hit/miss logic.

## Test Files

### FinalTest.ps1
Main test script that:
- Enables PowerShell experimental feature PSSubsystemPluginModel
- Builds Protocol and PredictorPlugin projects
- Loads the plugin module
- Configures PSReadLine for prediction

Run: `pwsh -File FinalTest.ps1`

### VerifyCache.ps1
Cache verification script that:
- Loads the plugin module
- Provides step-by-step cache testing instructions
- Explains how to observe cache hit/miss behavior through response times

Run: `pwsh -File VerifyCache.ps1`
