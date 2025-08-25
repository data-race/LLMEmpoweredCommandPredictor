using FluentAssertions;
using LLMEmpoweredCommandPredictor.Protocol.Server;
using LLMEmpoweredCommandPredictor.Protocol.Tests.Mocks;

namespace LLMEmpoweredCommandPredictor.Protocol.Tests.Server;

public class SuggestionServiceServerTests
{
    [Fact]
    public void Constructor_WithService_ShouldCreateServer()
    {
        // Arrange
        var service = new MockSuggestionService();

        // Act
        var server = new SuggestionServiceServer(service);

        // Assert
        server.Should().NotBeNull();
        server.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithServiceAndPipeName_ShouldCreateServer()
    {
        // Arrange
        var service = new MockSuggestionService();
        var pipeName = "TestPipe";

        // Act
        var server = new SuggestionServiceServer(service, pipeName);

        // Assert
        server.Should().NotBeNull();
        server.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => new SuggestionServiceServer(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullPipeName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = new MockSuggestionService();

        // Act & Assert
        Action act = () => new SuggestionServiceServer(service, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Dispose_ShouldNotThrowException()
    {
        // Arrange
        var service = new MockSuggestionService();
        var server = new SuggestionServiceServer(service);

        // Act & Assert
        server.Invoking(s => s.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrowException()
    {
        // Arrange
        var service = new MockSuggestionService();
        var server = new SuggestionServiceServer(service);

        // Act & Assert
        server.Invoking(s => s.Dispose()).Should().NotThrow();
        server.Invoking(s => s.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void Dispose_WhenDisposed_ShouldNotThrowException()
    {
        // Arrange
        var service = new MockSuggestionService();
        var server = new SuggestionServiceServer(service);
        server.Dispose();

        // Act & Assert
        server.Invoking(s => s.Dispose()).Should().NotThrow();
    }
}
