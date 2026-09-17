import createClient from "openapi-fetch";
import type { z } from "zod";
import type { paths } from "./api-types";
import { anomalySignalSchema } from "./schemas/anomaly-signal";
import type { PlatformMode } from "./schemas/common";
import { experimentRunSummarySchema } from "./schemas/experiment-run-summary";
import { healingOutcomeSchema } from "./schemas/healing-outcome";
import { healingPlanSchema } from "./schemas/healing-plan";
import { metricSampleSchema } from "./schemas/metric-sample";
import { modeResponseSchema } from "./schemas/mode-response";
import { topologyInfoSchema } from "./schemas/topology-info";

// Vazio = mesma origem (produção: Norn.API serve o dashboard via wwwroot, sem CORS — CLAUDE.md).
// Em dev, aponte VITE_API_BASE_URL pra porta real que `dotnet run --project src/Platform/Norn.API`
// imprimir (ver README deste pacote).
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

// `fetch` explícito (em vez do default de openapi-fetch, `globalThis.fetch` capturado na hora de
// criar o client) — o MSW intercepta reatribuindo `globalThis.fetch`, e como este módulo é
// importado antes do `server.listen()` dos testes rodar, um `fetch` capturado cedo demais fica
// permanentemente fora do alcance do MSW. Resolver por chamada garante o valor atual sempre.
const client = createClient<paths>({
  baseUrl,
  fetch: (...args: Parameters<typeof globalThis.fetch>) => globalThis.fetch(...args),
});

export class ApiError extends Error {
  status?: number;

  constructor(message: string, status?: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

/**
 * Todo REST passa pelo mesmo Zod que valida o SignalR (§7.3 pede Zod só no SignalR, mas os dois
 * canais carregam os mesmos tipos — ver docs/norn-api-contract.md) — é o que normaliza os campos
 * `number | string` que o OpenAPI gerado pra doubles/ints do .NET produz (NaN/Infinity de ponto
 * flutuante) para `number` de verdade, sem precisar de dois formatos de número no app.
 */
async function unwrap<TSchema extends z.ZodType>(
  promise: Promise<{ data?: unknown; response: Response; error?: unknown }>,
  schema: TSchema,
): Promise<z.infer<TSchema>> {
  const { data, response, error } = await promise;
  if (data === undefined) {
    throw new ApiError(
      `Falha na chamada à Norn.API (${response.status} ${response.statusText}): ${JSON.stringify(error)}`,
      response.status,
    );
  }
  return schema.parse(data);
}

export type { AnomalySignal } from "./schemas/anomaly-signal";
export type { HealingAction, PlatformMode } from "./schemas/common";
export type { ExperimentRunSummary } from "./schemas/experiment-run-summary";
export type { HealingOutcome } from "./schemas/healing-outcome";
export type { HealingPlan } from "./schemas/healing-plan";
export type { MetricSample } from "./schemas/metric-sample";
export type { TopologyInfo } from "./schemas/topology-info";

export function getTopology() {
  return unwrap(client.GET("/api/v1/topology"), topologyInfoSchema.array());
}

export function getSignals(params: { limit?: number; experimentRunId?: string } = {}) {
  return unwrap(
    client.GET("/api/v1/signals", {
      params: { query: { Limit: params.limit ?? 50, ExperimentRunId: params.experimentRunId } },
    }),
    anomalySignalSchema.array(),
  );
}

export function getPlans(params: { limit?: number } = {}) {
  return unwrap(
    client.GET("/api/v1/plans", { params: { query: { Limit: params.limit ?? 50 } } }),
    healingPlanSchema.array(),
  );
}

export function getOutcomes(params: { limit?: number } = {}) {
  return unwrap(
    client.GET("/api/v1/outcomes", { params: { query: { Limit: params.limit ?? 50 } } }),
    healingOutcomeSchema.array(),
  );
}

export function getMode() {
  return unwrap(client.GET("/api/v1/mode"), modeResponseSchema);
}

export function setMode(mode: PlatformMode) {
  return unwrap(client.PUT("/api/v1/mode", { body: { mode } }), modeResponseSchema);
}

export function getExperimentRun(runId: string) {
  return unwrap(
    client.GET("/api/v1/experiments/{runId}", { params: { path: { runId } } }),
    experimentRunSummarySchema,
  );
}

export function getMetricsSeries(params: {
  metricName: string;
  service: string;
  fromUtc: string;
  toUtc: string;
}) {
  return unwrap(
    client.GET("/api/v1/metrics/series", {
      params: {
        query: {
          MetricName: params.metricName,
          Service: params.service,
          FromUtc: params.fromUtc,
          ToUtc: params.toUtc,
        },
      },
    }),
    metricSampleSchema.array(),
  );
}
