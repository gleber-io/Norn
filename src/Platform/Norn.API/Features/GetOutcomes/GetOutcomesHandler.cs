using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetOutcomes;

public static class GetOutcomesHandler
{
    public static Task<IReadOnlyList<HealingOutcome>> HandleAsync(GetOutcomesRequest request, IKnowledgeReader reader, CancellationToken cancellationToken) =>
        reader.GetRecentOutcomesAsync(request.Limit, cancellationToken);
}
