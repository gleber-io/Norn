using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Norn.Planner.Barriers;
using Norn.Planner.LlmPlanning;
using Norn.Planner.Settings;
using Norn.Planner.Telemetry;
using Norn.Planner.Validation;
using OllamaSharp;
using RuleEngineImpl = Norn.Planner.RuleEngine.RuleEngine;

namespace Norn.Planner;

/// <summary>Composição do núcleo de decisão Norn.Planner — chamada pelo Norn.Worker (§4).</summary>
public static class PlannerServiceCollectionExtensions
{
    private const string HttpClientName = "Norn.Planner.Ollama";

    public static IServiceCollection AddNornPlanner(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PlannerOptions>()
            .Bind(configuration.GetSection(PlannerOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<PromptBudgetOptions>()
            .Bind(configuration.GetSection(PromptBudgetOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton(provider => provider.GetRequiredService<IOptions<PlannerOptions>>().Value);
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<PromptBudgetOptions>>().Value);

        services.AddHttpClient(HttpClientName, (provider, client) =>
        {
            var options = provider.GetRequiredService<PlannerOptions>();
            client.BaseAddress = new Uri(options.OllamaBaseUrl);

            // Sem timeout do lado do HttpClient — o teto real por tentativa (10s, §5.5) é um
            // CancellationTokenSource criado a cada chamada em LlmPlanner, não este.
            client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        });

        services.AddSingleton<IChatClient>(provider =>
        {
            var options = provider.GetRequiredService<PlannerOptions>();
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            return new OllamaApiClient(httpClient, options.ModelName);
        });

        services.AddSingleton<HealingActionPreconditionChecker>();
        services.AddSingleton<PromptBuilder>();
        services.AddSingleton<LlmOutputValidator>();
        services.AddSingleton<RuleEngineImpl>();
        services.AddSingleton<LlmPlanner>();
        services.AddSingleton<PlannerMetrics>();

        return services;
    }
}
