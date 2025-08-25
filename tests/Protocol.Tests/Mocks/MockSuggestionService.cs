using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation.Subsystem.Prediction;
using LLMEmpoweredCommandPredictor.Protocol.Contracts;
using LLMEmpoweredCommandPredictor.Protocol.Models;

namespace LLMEmpoweredCommandPredictor.Protocol.Tests.Mocks;

/// <summary>
/// Simple mock implementation of ISuggestionService for testing purposes.
/// </summary>
public class MockSuggestionService : ISuggestionService
{
    public Task<SuggestionResponse> GetSuggestionsAsync(
        SuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SuggestionResponse
        {
            Suggestions = new List<PredictiveSuggestion>(),
            Source = "mock",
            ConfidenceScore = 1.0
        });
    }

    public Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ServiceStatus
        {
            IsRunning = true,
            Uptime = TimeSpan.FromMinutes(1)
        });
    }

    public Task TriggerCacheRefreshAsync(
        SuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
