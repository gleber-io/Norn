using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Norn.BuildingBlocks.Web.FeatureFlags;

public static class FeatureFlagsServiceCollectionExtensions
{
    public static IServiceCollection AddNornFeatureFlags(this IServiceCollection services, IConnectionMultiplexer connectionMultiplexer)
    {
        services.AddMemoryCache();
        services.AddSingleton(connectionMultiplexer);
        services.AddSingleton<IFeatureFlags, RedisFeatureFlags>();
        services.AddHostedService<FeatureFlagInvalidationSubscriber>();

        return services;
    }
}
