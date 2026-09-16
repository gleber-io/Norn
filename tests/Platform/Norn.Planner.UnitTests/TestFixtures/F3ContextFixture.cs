using System.Text.Json;
using Norn.Contracts;

namespace Norn.Planner.UnitTests.TestFixtures;

/// <summary>
/// Fixture do §5.5, tarefa 11 — <c>AnomalyContext</c> real de uma execução de F3 (Fase 8, captura
/// ao vivo contra o <c>norn-qwen</c>/Ollama real), exportado de <c>anomaly_contexts</c> e
/// versionado em <c>Fixtures/f3-gateway-latency-context.json</c>. Um contexto montado à mão não
/// representa a entrada que o loop produz de verdade — daqui vem a garantia de procedência.
/// </summary>
internal static class F3ContextFixture
{
    /// <summary>
    /// <c>context_hash</c> gravado em <c>anomaly_contexts</c> no momento da captura — a asserção
    /// que transforma "este é o contexto real" de suposição em fato verificado.
    /// </summary>
    public const string ExpectedContextHash = "e0ce576bb389cf111b56b7cf51d2dc3e0bcb00d840c92e0014695e2c9f5e304f";

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static AnomalyContext Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "f3-gateway-latency-context.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AnomalyContext>(json, DeserializeOptions)
            ?? throw new InvalidOperationException("Fixture f3-gateway-latency-context.json desserializou para null.");
    }
}
