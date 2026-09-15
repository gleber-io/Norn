using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Norn.Contracts.Serialization;

/// <summary>
/// Serialização canônica (ADR-14, §5.3): chaves ordenadas, formatação numérica fixa. É a mesma
/// função usada para calcular <c>context_hash</c> (Norn.Knowledge, Fase 7) e <c>promptHash</c>
/// (Norn.Planner, Fase 8) — dois serializadores divergentes produziriam hash que não corresponde
/// ao que o modelo viu. Função pura: sem relógio, sem I/O.
/// </summary>
public static class CanonicalJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Serializa <paramref name="value"/> com chaves de objeto ordenadas alfabeticamente em todos os níveis.</summary>
    public static string Serialize<T>(T value)
    {
        var node = JsonSerializer.SerializeToNode(value, SerializerOptions);
        var canonicalNode = Canonicalize(node);

        return canonicalNode?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? "null";
    }

    /// <summary>SHA-256 do JSON canônico, em hex minúsculo.</summary>
    public static string ComputeHash<T>(T value) => ComputeHashOf(Serialize(value));

    /// <summary>SHA-256 de um JSON canônico já serializado (evita serializar duas vezes quando o chamador já tem o texto).</summary>
    public static string ComputeHashOf(string canonicalJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexStringLower(bytes);
    }

    private static JsonNode? Canonicalize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                var sorted = new JsonObject();
                foreach (var key in obj.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var child = obj[key];
                    sorted.Add(key, Canonicalize(child?.DeepClone()));
                }

                return sorted;

            case JsonArray array:
                var items = new JsonArray();
                foreach (var item in array)
                {
                    items.Add(Canonicalize(item?.DeepClone()));
                }

                return items;

            default:
                return node?.DeepClone();
        }
    }
}
