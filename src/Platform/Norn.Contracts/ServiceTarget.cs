namespace Norn.Contracts;

/// <summary>
/// Alvo de um sinal, contexto ou ação dentro do cluster. <c>PodUid</c> fica nulo onde a §5.3
/// não o exige (p.ex. <see cref="MetricSample"/>) e é obrigatório onde a corrida decisão↔atuação
/// importa (§5.4 — verificação de UID do <c>RestartPod</c>).
/// </summary>
public sealed record ServiceTarget
{
    public required string Service { get; init; }

    public required string Namespace { get; init; }

    public string? Pod { get; init; }

    public string? PodUid { get; init; }
}
