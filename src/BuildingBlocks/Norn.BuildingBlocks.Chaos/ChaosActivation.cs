namespace Norn.BuildingBlocks.Chaos;

/// <summary>
/// Registro de ativação lido do Redis. <see cref="FiredAtUtc"/> existe só para o F5: marca que o
/// kill já disparou para esta ativação, e impede o pod recriado pelo ReplicaSet de se matar de
/// novo em loop ao ler a mesma ativação ainda presente no Redis.
/// </summary>
public sealed record ChaosActivation(string ScenarioId, int Seed, DateTimeOffset ActivatedAtUtc, DateTimeOffset? FiredAtUtc = null);
