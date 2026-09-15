using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Norn.BuildingBlocks.Web.Routing;

/// <summary>Versionamento via prefixo de rota — nunca um pacote de API versioning (§7.1).</summary>
public static class RouteVersioningExtensions
{
    public static RouteGroupBuilder MapApiVersion(this IEndpointRouteBuilder endpoints, int version) =>
        endpoints.MapGroup($"/api/v{version}");
}
