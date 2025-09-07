# PredictorCache.Tests

## Overview

This test project contains unit tests for the **LLMEmpoweredCommandPredictor.PredictorCache** module.

### 📋 Test Classes

#### `CacheKeyGeneratorTests`
Tests the cache key generation logic that creates unique identifiers for cache entries based on user input and project context.

#### `InMemoryCacheTests`
Tests the in-memory cache implementation that stores LLM responses with TTL (Time-To-Live) and LRU (Least Recently Used) eviction.

## 🚀 Running Tests

### Prerequisites
- .NET 6.0 or later
- xUnit test framework

### Commands

```bash
dotnet build tests/PredictorCache.Tests/PredictorCache.Tests.csproj  
dotnet test tests/PredictorCache.Tests/PredictorCache.Tests.csproj 
```