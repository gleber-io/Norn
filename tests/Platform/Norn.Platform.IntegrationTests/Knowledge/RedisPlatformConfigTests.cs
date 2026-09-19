using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Norn.Contracts.Ports;
using Norn.Knowledge;
using Shouldly;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Norn.Platform.IntegrationTests.Knowledge;

/// <summary>
/// <see cref="IPlatformConfig.GetCurrentExperimentRunIdAsync"/>/<c>SetCurrentExperimentRunIdAsync</c>
/// contra Redis real (Testcontainers) — Fase 12. Achado ao vivo (smoke test da campanha): antes
/// deste par de métodos existir, <c>AnomalyPipelineBackgroundService</c> carimbava todo
/// <c>AnomalyContext</c> com <c>ExperimentRunId = null</c> hardcoded, e o join de
/// <c>Norn.PairedAnalysis</c> (H2) nunca batia com nada — silenciosamente, só descoberto ao rodar
/// a análise no fim de uma campanha de ~25h. Testado via Testcontainers (não dublê) porque a classe
/// não tem nenhum teste até aqui e o custo de uma regressão aqui é assimétrico: só aparece como H2
/// vazio no fim da campanha, não como falha visível no próximo ciclo (diferente de Mode/PlannerBackend).
/// Resolvido via <see cref="KnowledgeServiceCollectionExtensions.AddNornKnowledge"/> (não por
/// <c>new RedisPlatformConfig(...)</c> direto — a classe é <c>internal</c>) — a mesma composição
/// que Norn.Worker/Norn.API/Norn.Labeler usam de verdade, então o teste também cobre o registro de DI.
/// </summary>
public sealed class RedisPlatformConfigTests : IAsyncLifetime
{
    private readonly RedisContainer redisContainer = new RedisBuilder("redis:7-alpine").Build();
    private ServiceProvider serviceProvider = null!;
    private ConnectionMultiplexer connectionMultiplexer = null!;

    public async ValueTask InitializeAsync()
    {
        await redisContainer.StartAsync();
        connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(redisContainer.GetConnectionString());

        var services = new ServiceCollection();
        services.AddLogging();
        // "Knowledge" nunca é resolvida por nenhum teste aqui — AddDbContext é preguiçoso (só toca
        // a conexão quando KnowledgeDbContext é de fato usado), então uma connection string pra
        // Postgres nem precisa existir pra testar só a parte de Redis desta composição.
        var configuration = new ConfigurationBuilder().Build();
        services.AddNornKnowledge(configuration, connectionMultiplexer);
        serviceProvider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        connectionMultiplexer.Dispose();
        await serviceProvider.DisposeAsync();
        await redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task GetCurrentExperimentRunIdAsync_NoCampaignActive_ReturnsNull()
    {
        var platformConfig = serviceProvider.GetRequiredService<IPlatformConfig>();

        var result = await platformConfig.GetCurrentExperimentRunIdAsync(TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetCurrentExperimentRunIdAsync_ThenGet_RoundTripsTheGuid()
    {
        var platformConfig = serviceProvider.GetRequiredService<IPlatformConfig>();
        var runId = Guid.NewGuid();

        await platformConfig.SetCurrentExperimentRunIdAsync(runId, TestContext.Current.CancellationToken);
        var result = await platformConfig.GetCurrentExperimentRunIdAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(runId);
    }

    [Fact]
    public async Task SetCurrentExperimentRunIdAsync_Null_ClearsAPreviouslySetValue()
    {
        // Mesmo ciclo de vida real: Norn.Labeler init-run grava, label limpa (null) ao final de
        // uma única execução da campanha.
        var platformConfig = serviceProvider.GetRequiredService<IPlatformConfig>();
        await platformConfig.SetCurrentExperimentRunIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await platformConfig.SetCurrentExperimentRunIdAsync(null, TestContext.Current.CancellationToken);
        var result = await platformConfig.GetCurrentExperimentRunIdAsync(TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentExperimentRunIdAsync_AfterCacheExpiry_ReflectsValueWrittenByAnotherProcess()
    {
        // Testa o limite garantido, não o caminho rápido comum: em produção, SetCurrentExperimentRunIdAsync
        // publica em norn:platform:config:invalidate e o PlatformConfigInvalidationSubscriber do
        // Norn.Worker (hosted service real, registrado por AddNornKnowledge) costuma invalidar o
        // cache quase na hora — só que pub/sub do Redis é fire-and-forget, sem redelivery. O TTL de
        // 5s é a garantia de fato (pub/sub é otimização de latência por cima dela), e é isso que
        // este teste força: nenhum hosted service rodando aqui (BuildServiceProvider não inicia
        // IHostedService), só a expiração natural do cache pode fazer o "reader" enxergar a escrita
        // do "writer" — um cache que nunca expirasse mascararia justamente o bug desta sessão.
        var reader = serviceProvider.GetRequiredService<IPlatformConfig>();
        var writerServices = new ServiceCollection();
        writerServices.AddLogging();
        writerServices.AddNornKnowledge(new ConfigurationBuilder().Build(), connectionMultiplexer);
        await using var writerProvider = writerServices.BuildServiceProvider();
        var writer = writerProvider.GetRequiredService<IPlatformConfig>();

        await reader.GetCurrentExperimentRunIdAsync(TestContext.Current.CancellationToken); // popula o cache do "reader" com null
        var runId = Guid.NewGuid();
        await writer.SetCurrentExperimentRunIdAsync(runId, TestContext.Current.CancellationToken);

        // Cache de 5s (RedisPlatformConfig.CacheDuration, privado) — 6s garante que expirou.
        await Task.Delay(TimeSpan.FromSeconds(6), TestContext.Current.CancellationToken);
        var result = await reader.GetCurrentExperimentRunIdAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(runId);
    }
}
