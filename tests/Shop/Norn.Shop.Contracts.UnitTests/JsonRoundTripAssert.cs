using System.Text.Json;
using System.Text.Json.Serialization;
using Shouldly;

namespace Norn.Shop.Contracts.UnitTests;

internal static class JsonRoundTripAssert
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static void RoundTrips<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        var roundTripped = JsonSerializer.Deserialize<T>(json, Options);
        var roundTrippedJson = JsonSerializer.Serialize(roundTripped, Options);

        roundTrippedJson.ShouldBe(json);
    }
}
