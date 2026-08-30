using System.Net;

using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

/// <summary>
/// Tests for <see cref="DefaultLlmErrorClassifier"/>.
/// Verifies that per-turn timeouts, HTTP 5xx server errors, and HTTP 429
/// rate limits are classified as <see cref="EFailureClassification.TransientAdvance"/>.
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

    // --- Server errors → TransientAdvance ---

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

    // --- Rate limit errors → TransientAdvance ---

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
    public void Classify_UnrelatedRateLimitIdentifier_ReturnsPermanent()
    {
        // Arrange
        var ex = new Exception("invalid request: rate_limit_parameter is not accepted");

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.Permanent);
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
    public void Classify_ProviderCodeWithoutAdapterClassification_ReturnsPermanent()
    {
        // Arrange
        var ex = new LlmProviderException(
            new InvalidOperationException("provider capacity failure"),
            provider: "adapter-provider",
            errorCode: "capacity-code");

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.Permanent);
    }

    [Fact]
    public void Classify_ConfiguredProviderErrorMapping_ReturnsConfiguredClassification()
    {
        // Arrange
        var options = new LlmFailureClassificationOptions();
        options.ProviderErrorMappings[new LlmProviderErrorKey("adapter-provider", "capacity-code")] =
            EFailureClassification.TransientAdvance;
        var sut = new DefaultLlmErrorClassifier(options);
        var ex = new LlmProviderException(
            new InvalidOperationException("provider capacity failure"),
            provider: "ADAPTER-PROVIDER",
            errorCode: " capacity-code ");

        // Act
        var result = sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.TransientAdvance);
    }

    [Fact]
    public void Classify_ConfiguredProviderStatusMapping_ReturnsConfiguredClassification()
    {
        // Arrange
        var options = new LlmFailureClassificationOptions();
        options.ProviderStatusMappings[new LlmProviderStatusKey(
            "adapter-provider",
            (HttpStatusCode)529)] = EFailureClassification.TransientAdvance;
        var sut = new DefaultLlmErrorClassifier(options);
        var ex = new LlmProviderException(
            new InvalidOperationException("provider overload"),
            provider: "adapter-provider",
            statusCode: (HttpStatusCode)529);

        // Act
        var result = sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.TransientAdvance);
    }

    [Fact]
    public void Classify_ExplicitAdapterClassificationTakesPrecedenceOverConfiguredMapping()
    {
        // Arrange
        var options = new LlmFailureClassificationOptions();
        options.ProviderErrorMappings[new LlmProviderErrorKey("adapter-provider", "capacity-code")] =
            EFailureClassification.Permanent;
        var sut = new DefaultLlmErrorClassifier(options);
        var ex = new LlmProviderException(
            new InvalidOperationException("provider capacity failure"),
            provider: "adapter-provider",
            errorCode: "capacity-code",
            isTransient: true);

        // Act
        var result = sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.TransientAdvance);
    }

    [Fact]
    public void Classify_HardPermanentStatusTakesPrecedenceOverConfiguredMapping()
    {
        // Arrange
        var options = new LlmFailureClassificationOptions();
        options.ProviderStatusMappings[new LlmProviderStatusKey(
            "adapter-provider",
            HttpStatusCode.Unauthorized)] = EFailureClassification.TransientAdvance;
        var sut = new DefaultLlmErrorClassifier(options);
        var ex = new LlmProviderException(
            new InvalidOperationException("provider rejected the request"),
            provider: "adapter-provider",
            statusCode: HttpStatusCode.Unauthorized);

        // Act
        var result = sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.Permanent);
    }

    [Fact]
    public void Classify_WrappedCallerCancellation_ReturnsPermanent()
    {
        // Arrange
        _callerCts.Cancel();
        var ex = new LlmProviderException(
            new OperationCanceledException(),
            provider: "adapter-provider",
            errorCode: "capacity-code",
            isTransient: true);

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.Permanent);
    }

    [Theory]
    [InlineData(true, EFailureClassification.TransientAdvance)]
    [InlineData(false, EFailureClassification.Permanent)]
    public void Classify_ExplicitTransientMetadata_TakesPrecedenceWithoutHardStatus(
        bool isTransient,
        EFailureClassification expected)
    {
        // Arrange
        var ex = new LlmProviderException(
            new InvalidOperationException("explicit provider classification"),
            provider: "test-provider",
            isTransient: isTransient);

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout, EFailureClassification.TransientAdvance)]
    [InlineData(HttpStatusCode.TooManyRequests, EFailureClassification.TransientAdvance)]
    [InlineData(HttpStatusCode.InternalServerError, EFailureClassification.TransientAdvance)]
    [InlineData(HttpStatusCode.BadGateway, EFailureClassification.TransientAdvance)]
    [InlineData(HttpStatusCode.ServiceUnavailable, EFailureClassification.TransientAdvance)]
    [InlineData(HttpStatusCode.GatewayTimeout, EFailureClassification.TransientAdvance)]
    [InlineData(HttpStatusCode.BadRequest, EFailureClassification.Permanent)]
    [InlineData(HttpStatusCode.Unauthorized, EFailureClassification.Permanent)]
    [InlineData(HttpStatusCode.NotFound, EFailureClassification.Permanent)]
    [InlineData(HttpStatusCode.NotImplemented, EFailureClassification.Permanent)]
    [InlineData(HttpStatusCode.HttpVersionNotSupported, EFailureClassification.Permanent)]
    public void Classify_StructuredStatus_ReturnsExpectedClassification(
        HttpStatusCode statusCode,
        EFailureClassification expected)
    {
        // Arrange
        var ex = new LlmProviderException(
            new InvalidOperationException("provider status"),
            provider: "test-provider",
            statusCode: statusCode);

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Classify_UnclassifiedStatus529_ReturnsPermanent()
    {
        // Arrange
        var ex = new LlmProviderException(
            new InvalidOperationException("unclassified provider status"),
            provider: "adapter-provider",
            statusCode: (HttpStatusCode)529);

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.Permanent);
    }

    [Fact]
    public void Classify_ConflictingTransientMetadata_DoesNotOverrideHardPermanentStatus()
    {
        // Arrange
        var ex = new LlmProviderException(
            new InvalidOperationException("unauthorized"),
            provider: "adapter-provider",
            statusCode: HttpStatusCode.Unauthorized,
            isTransient: true);

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(EFailureClassification.Permanent);
    }

    [Theory]
    [InlineData("HTTP 599: provider unavailable", EFailureClassification.TransientAdvance)]
    [InlineData("HTTP 501: not implemented", EFailureClassification.Permanent)]
    [InlineData("HTTP 505: unsupported version", EFailureClassification.Permanent)]
    [InlineData("HTTP 5033: malformed status", EFailureClassification.Permanent)]
    public void Classify_LegacyHttpStatus_PreservesCompatibilityAndRejectsMalformedStatus(
        string message,
        EFailureClassification expected)
    {
        // Arrange
        var ex = new Exception(message);

        // Act
        var result = _sut.Classify(ex, _callerCts.Token);

        // Assert
        result.Should().Be(expected);
    }
}
