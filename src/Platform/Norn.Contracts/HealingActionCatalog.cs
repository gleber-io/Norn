namespace Norn.Contracts;

/// <summary>Descrição declarativa de uma entrada do catálogo fechado de ações (§5.4).</summary>
public sealed record ActionSpec(
    HealingActionType Type,
    string Description,
    IReadOnlyList<string> ParameterNames,
    string PreconditionDescription);

/// <summary>
/// Fonte única do catálogo de ações — o enum <see cref="HealingActionType"/> é a fonte de
/// verdade dos valores permitidos, este dicionário é a fonte de verdade da descrição de cada um
/// (§5.5, tarefa 3: "catálogo injetado a partir do enum, nunca hardcoded no texto"). Usado tanto
/// para montar o system prompt do LLM quanto para validar sua saída — os dois lados leem daqui,
/// nunca duplicam a descrição em texto solto.
/// </summary>
public static class HealingActionCatalog
{
    public static readonly IReadOnlyDictionary<HealingActionType, ActionSpec> Specs =
        new Dictionary<HealingActionType, ActionSpec>
        {
            [HealingActionType.ScaleUp] = new(
                HealingActionType.ScaleUp,
                "Aumenta o número de réplicas do Deployment alvo.",
                ["replicaDelta"],
                "réplicas atuais + replicaDelta ≤ maxReplicas; fora de cooldown"),
            [HealingActionType.RestartPod] = new(
                HealingActionType.RestartPod,
                "Reinicia o pod alvo — o ReplicaSet recria outro. Única ação irreversível do catálogo.",
                ["podName", "podUid"],
                "pod existe e o UID confere com o observado no contexto; máx. 1 restart do mesmo alvo por 10 min"),
            [HealingActionType.ToggleFeatureFlag] = new(
                HealingActionType.ToggleFeatureFlag,
                "Ativa ou desativa uma flag do catálogo do Shop.",
                ["flagName", "value"],
                $"flagName existe no catálogo de flags do Shop ({string.Join(", ", ShopFlagCatalog.All)})"),
            [HealingActionType.NoOp] = new(
                HealingActionType.NoOp,
                "Não atua — registra a decisão de não agir.",
                ["reason"],
                "nenhuma"),
        };
}
