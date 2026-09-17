import { z } from "zod";

/** Campos double/int do lado .NET podem sair como número ou string na borda do OpenAPI (NaN/Infinity
 * de ponto flutuante) — normaliza pra number de verdade, que é o que Recharts/cálculo no cliente
 * precisam. */
export const numeric = z.union([z.number(), z.string()]).transform(Number);

export const severitySchema = z.enum(["Low", "Medium", "High", "Critical"]);
export const detectorTypeSchema = z.enum(["SpikeDetection", "ChangePointDetection", "Forecast"]);
export const decidedBySchema = z.enum(["Llm", "RuleEngine", "Fallback"]);
export const healingActionTypeSchema = z.enum([
  "ScaleUp",
  "RestartPod",
  "ToggleFeatureFlag",
  "NoOp",
]);
export const healingOutcomeStatusSchema = z.enum([
  "Succeeded",
  "Failed",
  "PartiallyApplied",
  "Rejected",
  "TimedOut",
]);
export const platformModeSchema = z.enum(["Observe", "DryRun", "Active"]);
export type PlatformMode = z.infer<typeof platformModeSchema>;

export const serviceTargetSchema = z.object({
  service: z.string(),
  namespace: z.string(),
  pod: z.string().nullish(),
  podUid: z.string().nullish(),
});

export const timeWindowSchema = z.object({
  fromUtc: z.iso.datetime({ offset: true }),
  toUtc: z.iso.datetime({ offset: true }),
});

export const resourceSpecSchema = z.object({
  cpu: z.string(),
  memory: z.string(),
});

export const recentMetricsSchema = z.object({
  cpuUtilizationPct: numeric,
  memoryWorkingSetBytes: numeric,
  requestRatePerSecond: numeric,
  errorRatePct: numeric,
  latencyP99Ms: numeric,
  queueDepth: numeric,
  errorsByType: z.record(z.string(), numeric).optional(),
});

export const llmTraceSchema = z.object({
  promptHash: z.string().nullish(),
  model: z.string().nullish(),
  latencyMs: numeric.optional(),
  attempts: numeric.optional(),
  failureReasons: z.array(z.string()).optional(),
});

export const healingActionSchema = z.object({
  actionId: z.uuid(),
  type: healingActionTypeSchema,
  target: serviceTargetSchema,
  parameters: z.record(z.string(), z.string()).optional(),
  order: numeric,
  dryRun: z.boolean().optional(),
});
export type HealingAction = z.infer<typeof healingActionSchema>;
