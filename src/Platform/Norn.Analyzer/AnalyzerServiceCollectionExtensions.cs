using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Norn.Analyzer.Detection;
using Norn.Analyzer.Settings;
using Norn.Analyzer.SeverityCalculation;
using Norn.Analyzer.Telemetry;

namespace Norn.Analyzer;

/// <summary>Composição do núcleo de cálculo Norn.Analyzer — chamada pelo Norn.Worker (§4).</summary>
public static class AnalyzerServiceCollectionExtensions
{
    public static IServiceCollection AddNornAnalyzer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AnalyzerOptions>()
            .Bind(configuration.GetSection(AnalyzerOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<SeverityBandOptions>()
            .Bind(configuration.GetSection(SeverityBandOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton(provider => provider.GetRequiredService<IOptions<SeverityBandOptions>>().Value);
        services.AddSingleton<SeverityCalculator>();
        services.AddSingleton<AnalyzerMetrics>();
        services.AddSingleton<MetricDetectorEngine>();

        return services;
    }
}
