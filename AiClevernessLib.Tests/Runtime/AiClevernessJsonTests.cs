using System.Text.Json;

using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class AiClevernessJsonTests
{
    [Fact]
    public void CamelCase_SerializesPropertyNamesInCamelCase()
    {
        var payload = new SamplePayload("Ada", 3);

        var json = JsonSerializer.Serialize(payload, AiClevernessJson.CamelCase);

        json.Should().Contain("\"firstName\"");
        json.Should().Contain("\"itemCount\"");
    }

    [Fact]
    public void Default_PreservesPropertyCasing()
    {
        var payload = new SamplePayload("Ada", 3);

        var json = JsonSerializer.Serialize(payload, AiClevernessJson.Default);

        json.Should().Contain("\"FirstName\"");
        json.Should().Contain("\"ItemCount\"");
    }

    [Theory]
    [InlineData(typeof(List<string>))]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(double))]
    [InlineData(typeof(bool))]
    public void Context_RoundTripsAllRegisteredTypes(Type type)
    {
        var original = CreateSample(type);

        var json = JsonSerializer.Serialize(original, type, AiClevernessJson.Context);
        var roundTripped = JsonSerializer.Deserialize(json, type, AiClevernessJson.Context);

        // Serializing the deserialized value again must yield identical JSON,
        // proving the source-generated context supports the type losslessly.
        JsonSerializer.Serialize(roundTripped, type, AiClevernessJson.Context)
            .Should().Be(json);
    }

    [Fact]
    public void Context_RoundTripsDictionaryValuesSemantically()
    {
        // Dictionary values deserialize to JsonElement, which the source-generated
        // context does not register — so equality is asserted on the data itself.
        var json = JsonSerializer.Serialize(
            new Dictionary<string, object> { ["count"] = 42 },
            typeof(Dictionary<string, object>),
            AiClevernessJson.Context);

        var roundTripped = JsonSerializer.Deserialize(
            json,
            typeof(Dictionary<string, object>),
            AiClevernessJson.Context) as Dictionary<string, object>;

        roundTripped.Should().NotBeNull();
        ((JsonElement)roundTripped!["count"]).GetInt32().Should().Be(42);
    }

    private static object CreateSample(Type type)
    {
        if (type == typeof(List<string>)) return new List<string> { "alpha", "beta" };
        if (type == typeof(string)) return "sample";
        if (type == typeof(int)) return 42;
        if (type == typeof(long)) return 42_000_000_000L;
        if (type == typeof(double)) return 3.5;
        if (type == typeof(bool)) return true;

        throw new ArgumentException($"No sample registered for {type}", nameof(type));
    }

    private sealed record SamplePayload(string FirstName, int ItemCount);
}
