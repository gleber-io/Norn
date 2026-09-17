import { z } from "zod";
import { numeric } from "./common";

/** `GET /api/v1/experiments/{runId}` — não consumido por nenhuma feature ainda na Fase 11, mas
 * faz parte do contrato exposto pela Norn.API (fica pronto pra quando precisar). */
export const experimentRunSummarySchema = z.object({
  experimentRunId: z.uuid(),
  scenario: z.string(),
  arm: z.string(),
  repetition: numeric,
  runOrder: numeric,
  randomizationSeed: numeric,
  targetRps: numeric,
  achievedRps: numeric.nullish(),
  chaosSeed: numeric,
  loadSeed: numeric,
  injectionPhase: numeric,
  mode: z.string(),
  forecastEnabled: z.boolean(),
  forecastHorizonMinutes: numeric.nullish(),
  llmModelDigest: z.string().nullish(),
  llmTimeoutSeconds: numeric.nullish(),
  llmNumCtx: numeric.nullish(),
  startedAtUtc: z.iso.datetime({ offset: true }),
  onsetAtUtc: z.iso.datetime({ offset: true }).nullish(),
  windowEndAtUtc: z.iso.datetime({ offset: true }).nullish(),
  recoveredAtUtc: z.iso.datetime({ offset: true }).nullish(),
  terminationState: z.string().nullish(),
  cpuTempMaxCelsius: numeric.nullish(),
  cpuClockAvgMhz: numeric.nullish(),
  wslMemoryGb: numeric,
  wslProcessors: numeric,
  gitCommitSha: z.string(),
});

export type ExperimentRunSummary = z.infer<typeof experimentRunSummarySchema>;
