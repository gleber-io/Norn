import { z } from "zod";
import { healingOutcomeStatusSchema, numeric, recentMetricsSchema } from "./common";

/** Payload dos eventos SignalR `ActionApplied` e `OutcomeVerified` (§5.6) — os dois carregam o
 * mesmo `HealingOutcome`, achado confirmado na exploração da Fase 11: não são shapes diferentes. */
export const healingOutcomeSchema = z.object({
  outcomeId: z.uuid(),
  planId: z.uuid(),
  appliedAtUtc: z.iso.datetime({ offset: true }),
  verifiedAtUtc: z.iso.datetime({ offset: true }),
  status: healingOutcomeStatusSchema,
  sloRestored: z.boolean(),
  timeToRecoverySeconds: numeric,
  metricsBefore: recentMetricsSchema,
  metricsAfter: recentMetricsSchema,
  errorMessage: z.string().nullish(),
});

export type HealingOutcome = z.infer<typeof healingOutcomeSchema>;
