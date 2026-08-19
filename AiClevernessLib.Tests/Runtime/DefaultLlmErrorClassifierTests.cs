using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

/// <summary>
/// Tests for <see cref="DefaultLlmErrorClassifier"/>.
/// These tests expose the missing HTTP 5xx / 429 classification
/// (currently returns Permanent instead of TransientAdvance).
/// </summary>
public sealed class DefaultLlmErrorClassifierTests
{
    private readonly DefaultLlmErrorClassifier _sut = new();
    private readonly CancellationTokenSource _callerCts = new();

    // --- Existing behavior (should still pass) ---

    [Fact]
    public void Classify_Timeout_ReturnsTransientAdvance()
    {
        // Arrange — per-turn timeout: OperationCanceledException with caller token NOT cancelled
        var ex = new OperationCanceledException();

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.TransientAdvance);
    }

    [Fact]
    public void Classify_CallerCancellation_ReturnsPermanent()
    {
        // Arrange — user cancelled the operation
        _callerCts.Cancel();
        var ex = new OperationCanceledException();

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.Permanent);
    }

    [Fact]
    public void Classify_GenericError_ReturnsPermanent()
    {
        // Arrange
        var ex = new Exception("Something went wrong");

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.Permanent);
    }

    [Fact]
    public void Classify_ClientError_ReturnsPermanent()
    {
        // Arrange — HTTP 404 is not transient
        var ex = new Exception("HTTP 404: Not found");

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.Permanent);
    }

    // --- Server errors (BUG: currently returns Permanent, should return TransientAdvance) ---

    [Fact]
    public void Classify_ServerError503_ReturnsTransientAdvance()
    {
        // Arrange
        var ex = new Exception("HTTP 503: The model is temporarily unavailable");

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.TransientAdvance);
    }

    [Fact]
    public void Classify_ServerError502_ReturnsTransientAdvance()
    {
        // Arrange
        var ex = new Exception("HTTP 502: Bad gateway");

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.TransientAdvance);
    }

    [Fact]
    public void Classify_ServerError500_ReturnsTransientAdvance()
    {
        // Arrange
        var ex = new Exception("HTTP 500: Internal server error");

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.TransientAdvance);
    }

    // --- Rate limit errors (BUG: currently returns Permanent, should return TransientAdvance) ---

    [Fact]
    public void Classify_RateLimitHttp429_ReturnsTransientAdvance()
    {
        // Arrange
        var ex = new Exception("HTTP 429: Too many requests");

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.TransientAdvance);
    }

    [Fact]
    public void Classify_RateLimitExceededText_ReturnsTransientAdvance()
    {
        // Arrange
        var ex = new Exception("Rate limit exceeded for model xyz");

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.TransientAdvance);
    }

    [Fact]
    public void Classify_RateLimitSnakeCase_ReturnsTransientAdvance()
    {
        // Arrange
        var ex = new Exception("error: rate_limit reached");

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.TransientAdvance);
    }

    [Fact]
    public void Classify_TooManyRequestsText_ReturnsTransientAdvance()
    {
        // Arrange
        var ex = new Exception("Too many requests, please slow down");

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.TransientAdvance);
    }

    [Fact]
    public void Classify_FreeModelsPerMin_ReturnsTransientAdvance()
    {
        // Arrange — OpenRouter-specific rate limit pattern
        var ex = new Exception("free-models-per-min limit hit");

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.TransientAdvance);
    }
}
