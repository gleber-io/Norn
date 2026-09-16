using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetExperimentRun;

public static class GetExperimentRunHandler
{
    public static Task<ExperimentRunSummary?> HandleAsync(Guid runId, IKnowledgeReader reader, CancellationToken cancellationToken) =>
        reader.GetExperimentRunAsync(runId, cancellationToken);
}
