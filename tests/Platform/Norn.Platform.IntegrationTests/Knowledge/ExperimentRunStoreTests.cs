using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Knowledge;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Norn.Platform.IntegrationTests.Knowledge;

/// <summary>
/// ExperimentRunStore contra Postgres real (Testcontainers) — Fase 12. As colunas de controle
/// nascem no reset (<see cref="ExperimentRunRecord"/>) e as de rotulagem chegam depois, quando a
/// janela de observação já fechou (<see cref="ExperimentRunLabelingResult"/>); o teste cobre as
/// duas escritas sobre a mesma linha, na ordem real em que <c>run-experiment.ps1</c> e
/// <c>Norn.Labeler</c> as produzem.
/// </summary>
public sealed class ExperimentRunStoreTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private ServiceProvider serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        await postgresContainer.StartAsync();

        var services = new ServiceCollection();
        services.AddDbContext<KnowledgeDbContext>(options => options.UseNpgsql(postgresContainer.GetConnectionString()));
        services.AddScoped<IExperimentRunStore, ExperimentRunStore>();
        serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await serviceProvider.DisposeAsync();
        await postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_ThenUpdateLabelingResultAsync_PersistsBothHalvesOnSameRow()
    {
        var runId = Guid.NewGuid();
        var record = new ExperimentRunRecord
        {
            ExperimentRunId = runId,
            Scenario = "F1",
            Arm = "C",
            Repetition = 1,
            RunOrder = 7,
            RandomizationSeed = 42,
            TargetRps = 10,
            ChaosSeed = 1,
            LoadSeed = 2,
            InjectionPhase = 300,
            Mode = "Active",
            ForecastEnabled = false,
            StartedAtUtc = DateTimeOffset.UtcNow,
            WslMemoryGb = 8,
            WslProcessors = 10,
            GitCommitSha = "deadbeef",
        };
        using var scope = serviceProvider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IExperimentRunStore>();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();

        await store.CreateAsync(record, TestContext.Current.CancellationToken);

        var afterCreate = await dbContext.ExperimentRuns.SingleAsync(r => r.ExperimentRunId == runId, TestContext.Current.CancellationToken);
        afterCreate.TerminationState.ShouldBeNull();
        afterCreate.Arm.ShouldBe("C");

        // Truncado a milissegundos: timestamptz do Postgres guarda precisão de microssegundos,
        // e comparar DateTimeOffset.UtcNow (precisão de 100ns) direto contra o valor lido de volta
        // falharia por diferença sub-microssegundo invisível em qualquer formatação de log.
        var onset = TruncateToMilliseconds(record.StartedAtUtc.AddMinutes(6));
        var recovered = TruncateToMilliseconds(onset.AddMinutes(3));
        await store.UpdateLabelingResultAsync(new ExperimentRunLabelingResult
        {
            ExperimentRunId = runId,
            AchievedRps = 9.6,
            OnsetAtUtc = onset,
            WindowEndAtUtc = onset.AddMinutes(10),
            RecoveredAtUtc = recovered,
            TerminationState = "Recovered",
            CpuTempMaxCelsius = 78.5,
            CpuClockAvgMhz = 3200,
        }, TestContext.Current.CancellationToken);

        var afterLabel = await dbContext.ExperimentRuns.AsNoTracking().SingleAsync(r => r.ExperimentRunId == runId, TestContext.Current.CancellationToken);
        afterLabel.TerminationState.ShouldBe("Recovered");
        afterLabel.OnsetAtUtc.ShouldBe(onset);
        afterLabel.RecoveredAtUtc.ShouldBe(recovered);
        afterLabel.AchievedRps.ShouldBe(9.6);
        // As colunas de controle do reset sobrevivem à atualização de rotulagem — não são reescritas.
        afterLabel.Scenario.ShouldBe("F1");
        afterLabel.RunOrder.ShouldBe(7);
    }

    private static DateTimeOffset TruncateToMilliseconds(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerMillisecond), value.Offset);
}
