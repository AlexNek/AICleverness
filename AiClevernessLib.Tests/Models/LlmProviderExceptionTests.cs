using System.Net;
using System.Text.Json;

using AiCleverness.Models;

using FluentAssertions;

namespace AiClevernessLib.Tests.Models;

public sealed class LlmProviderExceptionTests
{
    [Fact]
    public void Constructor_PreservesInnerExceptionAndExposesMetadata()
    {
        // Arrange
        var inner = new InvalidOperationException("provider failure");
        var retryAfter = TimeSpan.FromSeconds(12);

        // Act
        var exception = new LlmProviderException(
            inner,
            provider: "test-provider",
            errorCode: "capacity_exhausted",
            statusCode: HttpStatusCode.ServiceUnavailable,
            retryAfter: retryAfter,
            isTransient: true);

        // Assert
        exception.Message.Should().Be(inner.Message);
        exception.InnerException.Should().BeSameAs(inner);
        exception.Provider.Should().Be("test-provider");
        exception.ErrorCode.Should().Be("capacity_exhausted");
        exception.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        exception.RetryAfter.Should().Be(retryAfter);
        exception.IsTransient.Should().BeTrue();
        exception.Metadata.Should().BeEquivalentTo(new LlmProviderFailureMetadata
        {
            Provider = "test-provider",
            ErrorCode = "capacity_exhausted",
            StatusCode = HttpStatusCode.ServiceUnavailable,
            RetryAfter = retryAfter
        });
    }

    [Fact]
    public void MetadataProjection_IsImmutableByReplacement()
    {
        // Arrange
        var exception = new LlmProviderException(
            new InvalidOperationException("provider failure"),
            provider: "test-provider");

        // Act
        var replacement = exception.Metadata with { Provider = "other-provider" };

        // Assert
        replacement.Provider.Should().Be("other-provider");
        exception.Metadata.Provider.Should().Be("test-provider");
    }

    [Fact]
    public void Constructor_NullInnerException_Throws()
    {
        // Act
        var act = () => new LlmProviderException(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("exception");
    }

    [Fact]
    public void DiagnosticContracts_DefaultMetadataToNull_AndPreserveRecordEquality()
    {
        // Arrange
        var timestamp = DateTimeOffset.Parse("2026-01-02T03:04:05+00:00");
        var callInfo = new LlmCallInfo
        {
            Attempt = 1,
            Duration = TimeSpan.FromSeconds(1),
            ExecutionId = "exec-1",
            Model = "model-a",
            StartedAt = timestamp,
            Success = false,
            Turn = 0
        };
        var equivalentCallInfo = callInfo with { };
        var busEvent = new LlmCallCompletedBusEvent(
            "exec-1",
            TimeSpan.FromSeconds(1),
            null,
            Success: false,
            Turn: 0,
            Error: "provider failure")
        {
            Timestamp = timestamp
        };
        var equivalentBusEvent = busEvent with { };
        var failureEvent = new FailureEvent
        {
            ExecutionId = "exec-1",
            Timestamp = timestamp,
            Error = "provider failure"
        };
        var equivalentFailureEvent = failureEvent with { };

        // Assert
        callInfo.ProviderFailure.Should().BeNull();
        callInfo.Should().Be(equivalentCallInfo);
        busEvent.ProviderFailure.Should().BeNull();
        busEvent.Should().Be(equivalentBusEvent);
        failureEvent.ProviderFailure.Should().BeNull();
        failureEvent.Should().Be(equivalentFailureEvent);
    }

    [Fact]
    public void BusEvent_DeserializesWithoutProviderMetadata()
    {
        // Arrange — this represents a payload written before Feature 10.
        const string json = "{\"ExecutionId\":\"exec-1\",\"Duration\":\"00:00:01\",\"Usage\":null,\"Success\":false,\"Turn\":0,\"Error\":\"provider failure\"}";

        // Act
        var deserialized = JsonSerializer.Deserialize<LlmCallCompletedBusEvent>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ExecutionId.Should().Be("exec-1");
        deserialized.ProviderFailure.Should().BeNull();
        deserialized.Success.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NegativeRetryAfter_Throws()
    {
        // Act
        var act = () => new LlmProviderException(
            new InvalidOperationException("provider failure"),
            retryAfter: TimeSpan.FromSeconds(-1));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("retryAfter");
    }
}
