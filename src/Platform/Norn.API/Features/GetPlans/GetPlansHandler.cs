using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetPlans;

public static class GetPlansHandler
{
    public static Task<IReadOnlyList<HealingPlan>> HandleAsync(GetPlansRequest request, IKnowledgeReader reader, CancellationToken cancellationToken) =>
        reader.GetRecentPlansAsync(request.Limit, cancellationToken);
}
