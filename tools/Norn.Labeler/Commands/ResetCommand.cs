using Microsoft.Extensions.DependencyInjection;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Labeler.Cli;

namespace Norn.Labeler.Commands;

/// <summary>
/// Fase 12, tarefa 2a — reset de estado antes de cada execução. Zera toda a árvore
/// <c>shop:flags:</c> e grava modo + <see cref="PlannerBackend"/> conforme o braço, pelos
/// adaptadores reais (invalidação pub/sub inclusa) — nunca por escrita direta de chave Redis, que
/// duplicaria o formato e esqueceria de publicar a invalidação. K8s (réplicas de baseline) e a
/// árvore <c>norn:platform:config:forecast</c> são resetados pelo próprio <c>run-experiment.ps1</c>
/// via <c>kubectl</c> — não precisam de um adaptador .NET.
/// </summary>
public static class ResetCommand
{
    public static async Task RunAsync(string[] args, IServiceProvider services, CancellationToken cancellationToken)
    {
        var arm = ArgReader.GetRequiredSetting(args, "--arm");
        var (mode, backend) = arm switch
        {
            "A" => (PlatformMode.Observe, PlannerBackend.RuleEngine),
            "B" => (PlatformMode.Active, PlannerBackend.Llm),
            "C" => (PlatformMode.Active, PlannerBackend.RuleEngine),
            _ => throw new ArgumentException($"Braço desconhecido: {arm} (esperado A, B ou C)"),
        };

        var flagWriter = services.GetRequiredService<IFeatureFlagWriter>();
        foreach (var flag in ShopFlagCatalog.All)
        {
            await flagWriter.SetAsync(flag, false, cancellationToken);
        }

        var platformConfig = services.GetRequiredService<IPlatformConfig>();
        await platformConfig.SetModeAsync(mode, cancellationToken);
        await platformConfig.SetPlannerBackendAsync(backend, cancellationToken);

        // Lido de volta, não assumido — run-experiment.ps1 assere que corresponde ao braço pedido
        // antes de prosseguir (§3, tarefa 2a: "execução que falhe nessa conferência é descartada e refeita").
        var confirmedMode = await platformConfig.GetModeAsync(cancellationToken);
        var confirmedBackend = await platformConfig.GetPlannerBackendAsync(cancellationToken);
        Console.WriteLine($"mode={confirmedMode}");
        Console.WriteLine($"plannerBackend={confirmedBackend}");
    }
}
