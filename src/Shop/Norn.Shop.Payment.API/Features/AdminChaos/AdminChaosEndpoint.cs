using Microsoft.AspNetCore.Http.HttpResults;
using Norn.BuildingBlocks.Chaos;
using Norn.BuildingBlocks.Web.Validation;

namespace Norn.Shop.Payment.API.Features.AdminChaos;

/// <summary>
/// Ativação de cenário de caos por ID e seed (§8, Fase 5, tarefa 3). Mapeado só em ambiente
/// não-produtivo — a checagem é responsabilidade do composition root (<c>Program.cs</c>), não
/// desta slice.
/// </summary>
public static class AdminChaosEndpoint
{
    public static IEndpointRouteBuilder MapAdminChaos(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/chaos");

        group.MapPost("/activate", async Task<NoContent> (
                AdminChaosActivateRequest request,
                IChaosActivationStore store,
                CancellationToken cancellationToken) =>
            {
                await AdminChaosHandler.ActivateAsync(request, store, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithValidation<AdminChaosActivateRequest>()
            .WithName("ActivateChaos");

        group.MapPost("/deactivate", async Task<NoContent> (
                IChaosActivationStore store,
                CancellationToken cancellationToken) =>
            {
                await AdminChaosHandler.DeactivateAsync(store, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("DeactivateChaos");

        return app;
    }
}
