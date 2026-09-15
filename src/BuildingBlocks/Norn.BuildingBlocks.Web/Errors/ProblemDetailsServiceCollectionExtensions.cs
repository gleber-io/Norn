using Microsoft.Extensions.DependencyInjection;

namespace Norn.BuildingBlocks.Web.Errors;

public static class ProblemDetailsServiceCollectionExtensions
{
    public static IServiceCollection AddNornProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<NornExceptionHandler>();

        return services;
    }
}
