namespace Norn.BuildingBlocks.Chaos.Effects;

/// <summary>
/// F5 — mata o processo imediatamente. <see cref="ChaosActivation.FiredAtUtc"/> é gravado no Redis
/// *antes* do kill: sem essa marca, o pod recriado pelo ReplicaSet leria a mesma ativação ainda
/// presente e se mataria de novo, em loop — a barreira aqui é análoga ao cooldown do ADR-04, só
/// que para um cenário de caos, não para uma ação de cura.
/// </summary>
internal sealed class AbruptKillEffect(IChaosActivationStore activationStore) : ChaosEffectBase
{
    public override string ScenarioId => ChaosScenarioIds.F5;

    public override async ValueTask TickAsync(ChaosActivation activation, double intensity, CancellationToken cancellationToken)
    {
        if (activation.FiredAtUtc is not null)
        {
            return;
        }

        await activationStore.MarkFiredAsync(DateTimeOffset.UtcNow, cancellationToken);
        Environment.Exit(137);
    }
}
