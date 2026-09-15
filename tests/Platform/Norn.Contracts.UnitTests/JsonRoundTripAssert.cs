using System.Text.Json;
using System.Text.Json.Serialization;
using Shouldly;

namespace Norn.Contracts.UnitTests;

/// <summary>
/// Compara o JSON serializado do original com o JSON do valor desserializado, em vez de
/// igualdade de objeto — records com propriedades <c>IReadOnlyList</c>/<c>IReadOnlyDictionary</c>
/// não têm igualdade estrutural por padrão, então comparar instâncias daria falso negativo.
/// </summary>
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
