using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Norn.Knowledge.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "platform");

        migrationBuilder.CreateTable(
            name: "anomaly_signals",
            schema: "platform",
            columns: table => new
            {
                signal_id = table.Column<Guid>(type: "uuid", nullable: false),
                detected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                experiment_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                service = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                metric_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_anomaly_signals", x => x.signal_id);
            });

        migrationBuilder.CreateTable(
            name: "experiment_runs",
            schema: "platform",
            columns: table => new
            {
                experiment_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                scenario = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                arm = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                repetition = table.Column<int>(type: "integer", nullable: false),
                run_order = table.Column<int>(type: "integer", nullable: false),
                randomization_seed = table.Column<int>(type: "integer", nullable: false),
                target_rps = table.Column<double>(type: "double precision", nullable: false),
                achieved_rps = table.Column<double>(type: "double precision", nullable: true),
                chaos_seed = table.Column<int>(type: "integer", nullable: false),
                load_seed = table.Column<int>(type: "integer", nullable: false),
                injection_phase = table.Column<double>(type: "double precision", nullable: false),
                mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                forecast_enabled = table.Column<bool>(type: "boolean", nullable: false),
                forecast_horizon_minutes = table.Column<int>(type: "integer", nullable: true),
                llm_model_digest = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                llm_timeout_seconds = table.Column<int>(type: "integer", nullable: true),
                llm_num_ctx = table.Column<int>(type: "integer", nullable: true),
                started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                onset_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                window_end_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                recovered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                termination_state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                cpu_temp_max_celsius = table.Column<double>(type: "double precision", nullable: true),
                cpu_clock_avg_mhz = table.Column<double>(type: "double precision", nullable: true),
                wsl_memory_gb = table.Column<int>(type: "integer", nullable: false),
                wsl_processors = table.Column<int>(type: "integer", nullable: false),
                git_commit_sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_experiment_runs", x => x.experiment_run_id);
            });

        migrationBuilder.CreateTable(
            name: "anomaly_contexts",
            schema: "platform",
            columns: table => new
            {
                context_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                experiment_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                service = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                primary_signal_id = table.Column<Guid>(type: "uuid", nullable: false),
                context_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_anomaly_contexts", x => x.context_id);
                table.ForeignKey(
                    name: "FK_anomaly_contexts_anomaly_signals_primary_signal_id",
                    column: x => x.primary_signal_id,
                    principalSchema: "platform",
                    principalTable: "anomaly_signals",
                    principalColumn: "signal_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "healing_plans",
            schema: "platform",
            columns: table => new
            {
                plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                context_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                decided_by = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_healing_plans", x => x.plan_id);
                table.ForeignKey(
                    name: "FK_healing_plans_anomaly_contexts_context_id",
                    column: x => x.context_id,
                    principalSchema: "platform",
                    principalTable: "anomaly_contexts",
                    principalColumn: "context_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "healing_outcomes",
            schema: "platform",
            columns: table => new
            {
                outcome_id = table.Column<Guid>(type: "uuid", nullable: false),
                plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                applied_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                verified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_healing_outcomes", x => x.outcome_id);
                table.ForeignKey(
                    name: "FK_healing_outcomes_healing_plans_plan_id",
                    column: x => x.plan_id,
                    principalSchema: "platform",
                    principalTable: "healing_plans",
                    principalColumn: "plan_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "llm_traces",
            schema: "platform",
            columns: table => new
            {
                trace_id = table.Column<Guid>(type: "uuid", nullable: false),
                plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                prompt_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                latency_ms = table.Column<long>(type: "bigint", nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_llm_traces", x => x.trace_id);
                table.ForeignKey(
                    name: "FK_llm_traces_healing_plans_plan_id",
                    column: x => x.plan_id,
                    principalSchema: "platform",
                    principalTable: "healing_plans",
                    principalColumn: "plan_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_anomaly_contexts_experiment_run_id",
            schema: "platform",
            table: "anomaly_contexts",
            column: "experiment_run_id");

        migrationBuilder.CreateIndex(
            name: "IX_anomaly_contexts_primary_signal_id",
            schema: "platform",
            table: "anomaly_contexts",
            column: "primary_signal_id");

        migrationBuilder.CreateIndex(
            name: "IX_anomaly_contexts_service_created_at_utc",
            schema: "platform",
            table: "anomaly_contexts",
            columns: new[] { "service", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_anomaly_signals_experiment_run_id",
            schema: "platform",
            table: "anomaly_signals",
            column: "experiment_run_id");

        migrationBuilder.CreateIndex(
            name: "IX_anomaly_signals_service_metric_name_detected_at_utc",
            schema: "platform",
            table: "anomaly_signals",
            columns: new[] { "service", "metric_name", "detected_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_experiment_runs_scenario_arm_repetition",
            schema: "platform",
            table: "experiment_runs",
            columns: new[] { "scenario", "arm", "repetition" });

        migrationBuilder.CreateIndex(
            name: "IX_healing_outcomes_plan_id",
            schema: "platform",
            table: "healing_outcomes",
            column: "plan_id");

        migrationBuilder.CreateIndex(
            name: "IX_healing_plans_context_id",
            schema: "platform",
            table: "healing_plans",
            column: "context_id");

        migrationBuilder.CreateIndex(
            name: "IX_llm_traces_plan_id",
            schema: "platform",
            table: "llm_traces",
            column: "plan_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "experiment_runs",
            schema: "platform");

        migrationBuilder.DropTable(
            name: "healing_outcomes",
            schema: "platform");

        migrationBuilder.DropTable(
            name: "llm_traces",
            schema: "platform");

        migrationBuilder.DropTable(
            name: "healing_plans",
            schema: "platform");

        migrationBuilder.DropTable(
            name: "anomaly_contexts",
            schema: "platform");

        migrationBuilder.DropTable(
            name: "anomaly_signals",
            schema: "platform");
    }
}
