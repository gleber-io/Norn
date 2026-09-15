using OpenTelemetry;

namespace Norn.BuildingBlocks.Telemetry;

/// <summary>
/// <c>ExperimentRunId</c> propagado como baggage em todos os sinais (§7.5) — permite que o
/// Collector e qualquer serviço na cadeia leiam o experimento em curso sem replicar o valor
/// em cada assinatura de método.
/// </summary>
public static class NornBaggage
{
    private const string ExperimentRunIdKey = "experimentRunId";

    public static void SetExperimentRunId(Guid experimentRunId) =>
        Baggage.SetBaggage(ExperimentRunIdKey, experimentRunId.ToString());

    public static Guid? GetExperimentRunId() =>
        Guid.TryParse(Baggage.GetBaggage(ExperimentRunIdKey), out var value) ? value : null;
}
