using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.API.Features.GetTopology;

public static class GetTopologyHandler
{
    public static Task<IReadOnlyList<TopologyInfo>> HandleAsync(IKnowledgeReader reader, CancellationToken cancellationToken) =>
        reader.GetLatestTopologyAsync(cancellationToken);
}
