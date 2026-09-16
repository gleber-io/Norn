using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.Executor.FeatureFlags;

/// <summary>Aplica <c>ToggleFeatureFlag</c> de verdade — <c>SET</c> + publicação de invalidação (§5.4, §5.7).</summary>
public sealed class FeatureFlagActionApplier(IFeatureFlagWriter featureFlagWriter)
{
    public async Task<ActionApplyResult> ApplyAsync(HealingAction action, CancellationToken cancellationToken)
    {
        var flagName = action.Parameters["flagName"];
        if (!bool.TryParse(action.Parameters.GetValueOrDefault("value"), out var value))
        {
            return ActionApplyResult.Rejected("value ausente ou não booleano na ação de ToggleFeatureFlag.");
        }

        try
        {
            await featureFlagWriter.SetAsync(flagName, value, cancellationToken);
            return ActionApplyResult.Succeeded();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ActionApplyResult.Failed(ex.Message);
        }
    }
}
