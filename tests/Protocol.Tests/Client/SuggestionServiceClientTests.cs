using FluentAssertions;
using LLMEmpoweredCommandPredictor.Protocol.Client;
using LLMEmpoweredCommandPredictor.Protocol.Models;

namespace LLMEmpoweredCommandPredictor.Protocol.Tests.Client;

public class SuggestionServiceClientTests
{
    [Fact]
    public void Constructor_WithNullSettings_ShouldUseDefaultSettings()
    {
        // Act
        var client = new SuggestionServiceClient(null);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithSettings_ShouldUseProvidedSettings()
    {
        // Arrange
        var settings = new ConnectionSettings
        {
            TimeoutMs = 20,
            EnableDebugLogging = true
        };

        // Act
        var client = new SuggestionServiceClient(settings);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSuggestionsAsync_WhenServiceUnavailable_ShouldReturnEmptyResponseAsync()
    {
        // Arrange
        var client = new SuggestionServiceClient();
        var request = new SuggestionRequest
        {
            UserInput = "test"
        };

        // Act
        var response = await client.GetSuggestionsAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Suggestions.Should().BeEmpty();
        response.Source.Should().Be("fallback");
        response.ConfidenceScore.Should().Be(0.0);
        response.WarningMessage.Should().Be("Service unavailable");
    }

    [Fact]
    public async Task GetSuggestionsAsync_WithCancellation_ShouldReturnCancelledResponseAsync()
    {
        // Arrange
        var client = new SuggestionServiceClient();
        var request = new SuggestionRequest
        {
            UserInput = "test"
        };
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var response = await client.GetSuggestionsAsync(request, cts.Token);

        // Assert
        response.Should().NotBeNull();
        response.Suggestions.Should().BeEmpty();
        response.Source.Should().Be("fallback");
        response.ConfidenceScore.Should().Be(0.0);
        response.WarningMessage.Should().Be("Service unavailable");
    }

    [Fact]
    public void Dispose_ShouldNotThrowException()
    {
        // Arrange
        var client = new SuggestionServiceClient();

        // Act & Assert
        client.Invoking(c => c.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrowException()
    {
        // Arrange
        var client = new SuggestionServiceClient();

        // Act & Assert
        client.Invoking(c => c.Dispose()).Should().NotThrow();
        client.Invoking(c => c.Dispose()).Should().NotThrow();
    }
}
