using Microsoft.EntityFrameworkCore;
using Norn.Knowledge.Entities;

namespace Norn.Knowledge;

/// <summary>
/// Schema <c>platform</c> — separado do schema por serviço do Shop (mesmo Postgres, ADR-06),
/// como o Catalog.API já faz com <c>catalog</c> (Fase 2). Histórico auditável (sinais, contextos,
/// planos, resultados, traces de LLM, execuções da campanha) — append-only, nunca consultado
/// relacionalmente além de execução, alvo e tempo.
/// </summary>
public sealed class KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options) : DbContext(options)
{
    public DbSet<AnomalySignalRow> AnomalySignals => Set<AnomalySignalRow>();

    public DbSet<AnomalyContextRow> AnomalyContexts => Set<AnomalyContextRow>();

    public DbSet<HealingPlanRow> HealingPlans => Set<HealingPlanRow>();

    public DbSet<LlmTraceRow> LlmTraces => Set<LlmTraceRow>();

    public DbSet<HealingOutcomeRow> HealingOutcomes => Set<HealingOutcomeRow>();

    public DbSet<ExperimentRunRow> ExperimentRuns => Set<ExperimentRunRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("platform");

        modelBuilder.Entity<AnomalySignalRow>(entity =>
        {
            entity.ToTable("anomaly_signals");
            entity.HasKey(row => row.SignalId);
            entity.Property(row => row.SignalId).HasColumnName("signal_id");
            entity.Property(row => row.DetectedAtUtc).HasColumnName("detected_at_utc");
            entity.Property(row => row.ExperimentRunId).HasColumnName("experiment_run_id");
            entity.Property(row => row.Service).HasColumnName("service").HasMaxLength(200);
            entity.Property(row => row.MetricName).HasColumnName("metric_name").HasMaxLength(200);
            entity.Property(row => row.Severity).HasColumnName("severity").HasMaxLength(20);
            entity.Property(row => row.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.HasIndex(row => new { row.Service, row.MetricName, row.DetectedAtUtc });
            entity.HasIndex(row => row.ExperimentRunId);
        });

        modelBuilder.Entity<AnomalyContextRow>(entity =>
        {
            entity.ToTable("anomaly_contexts");
            entity.HasKey(row => row.ContextId);
            entity.Property(row => row.ContextId).HasColumnName("context_id");
            entity.Property(row => row.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(row => row.CorrelationId).HasColumnName("correlation_id");
            entity.Property(row => row.ExperimentRunId).HasColumnName("experiment_run_id");
            entity.Property(row => row.Service).HasColumnName("service").HasMaxLength(200);
            entity.Property(row => row.PrimarySignalId).HasColumnName("primary_signal_id");
            entity.Property(row => row.ContextHash).HasColumnName("context_hash").HasMaxLength(64);
            entity.Property(row => row.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.HasOne<AnomalySignalRow>().WithMany().HasForeignKey(row => row.PrimarySignalId);
            entity.HasIndex(row => row.ExperimentRunId);
            entity.HasIndex(row => new { row.Service, row.CreatedAtUtc });
        });

        modelBuilder.Entity<HealingPlanRow>(entity =>
        {
            entity.ToTable("healing_plans");
            entity.HasKey(row => row.PlanId);
            entity.Property(row => row.PlanId).HasColumnName("plan_id");
            entity.Property(row => row.ContextId).HasColumnName("context_id");
            entity.Property(row => row.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(row => row.DecidedBy).HasColumnName("decided_by").HasMaxLength(20);
            entity.Property(row => row.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.HasOne<AnomalyContextRow>().WithMany().HasForeignKey(row => row.ContextId);
        });

        modelBuilder.Entity<LlmTraceRow>(entity =>
        {
            entity.ToTable("llm_traces");
            entity.HasKey(row => row.TraceId);
            entity.Property(row => row.TraceId).HasColumnName("trace_id");
            entity.Property(row => row.PlanId).HasColumnName("plan_id");
            entity.Property(row => row.PromptHash).HasColumnName("prompt_hash").HasMaxLength(64);
            entity.Property(row => row.Model).HasColumnName("model").HasMaxLength(200);
            entity.Property(row => row.LatencyMs).HasColumnName("latency_ms");
            entity.Property(row => row.Attempts).HasColumnName("attempts");
            entity.Property(row => row.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.HasOne<HealingPlanRow>().WithMany().HasForeignKey(row => row.PlanId);
        });

        modelBuilder.Entity<HealingOutcomeRow>(entity =>
        {
            entity.ToTable("healing_outcomes");
            entity.HasKey(row => row.OutcomeId);
            entity.Property(row => row.OutcomeId).HasColumnName("outcome_id");
            entity.Property(row => row.PlanId).HasColumnName("plan_id");
            entity.Property(row => row.AppliedAtUtc).HasColumnName("applied_at_utc");
            entity.Property(row => row.VerifiedAtUtc).HasColumnName("verified_at_utc");
            entity.Property(row => row.Status).HasColumnName("status").HasMaxLength(30);
            entity.Property(row => row.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.HasOne<HealingPlanRow>().WithMany().HasForeignKey(row => row.PlanId);
        });

        modelBuilder.Entity<ExperimentRunRow>(entity =>
        {
            entity.ToTable("experiment_runs");
            entity.HasKey(row => row.ExperimentRunId);
            entity.Property(row => row.ExperimentRunId).HasColumnName("experiment_run_id");
            entity.Property(row => row.Scenario).HasColumnName("scenario").HasMaxLength(10);
            entity.Property(row => row.Arm).HasColumnName("arm").HasMaxLength(1);
            entity.Property(row => row.Repetition).HasColumnName("repetition");
            entity.Property(row => row.RunOrder).HasColumnName("run_order");
            entity.Property(row => row.RandomizationSeed).HasColumnName("randomization_seed");
            entity.Property(row => row.TargetRps).HasColumnName("target_rps");
            entity.Property(row => row.AchievedRps).HasColumnName("achieved_rps");
            entity.Property(row => row.ChaosSeed).HasColumnName("chaos_seed");
            entity.Property(row => row.LoadSeed).HasColumnName("load_seed");
            entity.Property(row => row.InjectionPhase).HasColumnName("injection_phase");
            entity.Property(row => row.Mode).HasColumnName("mode").HasMaxLength(20);
            entity.Property(row => row.ForecastEnabled).HasColumnName("forecast_enabled");
            entity.Property(row => row.ForecastHorizonMinutes).HasColumnName("forecast_horizon_minutes");
            entity.Property(row => row.LlmModelDigest).HasColumnName("llm_model_digest").HasMaxLength(200);
            entity.Property(row => row.LlmTimeoutSeconds).HasColumnName("llm_timeout_seconds");
            entity.Property(row => row.LlmNumCtx).HasColumnName("llm_num_ctx");
            entity.Property(row => row.StartedAtUtc).HasColumnName("started_at_utc");
            entity.Property(row => row.OnsetAtUtc).HasColumnName("onset_at_utc");
            entity.Property(row => row.WindowEndAtUtc).HasColumnName("window_end_at_utc");
            entity.Property(row => row.RecoveredAtUtc).HasColumnName("recovered_at_utc");
            entity.Property(row => row.TerminationState).HasColumnName("termination_state").HasMaxLength(30);
            entity.Property(row => row.CpuTempMaxCelsius).HasColumnName("cpu_temp_max_celsius");
            entity.Property(row => row.CpuClockAvgMhz).HasColumnName("cpu_clock_avg_mhz");
            entity.Property(row => row.WslMemoryGb).HasColumnName("wsl_memory_gb");
            entity.Property(row => row.WslProcessors).HasColumnName("wsl_processors");
            entity.Property(row => row.GitCommitSha).HasColumnName("git_commit_sha").HasMaxLength(40);
            entity.HasIndex(row => new { row.Scenario, row.Arm, row.Repetition });
        });
    }
}
