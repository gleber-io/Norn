using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetSignals;

public static class GetSignalsHandler
{
    public static Task<IReadOnlyList<AnomalySignal>> HandleAsync(GetSignalsRequest request, IKnowledgeReader reader, CancellationToken cancellationToken) =>
        reader.GetRecentSignalsAsync(request.Limit, request.ExperimentRunId, cancellationToken);
}
