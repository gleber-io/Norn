import { z } from "zod";
import { decidedBySchema, healingActionSchema, llmTraceSchema, numeric } from "./common";

/** Payload do evento SignalR `PlanCreated` (§5.6) — mesma forma de um item de `GET /api/v1/plans`. */
export const healingPlanSchema = z.object({
  planId: z.uuid(),
  contextId: z.uuid(),
  createdAtUtc: z.iso.datetime({ offset: true }),
  decidedBy: decidedBySchema,
  rationale: z.string(),
  confidence: numeric,
  actions: z.array(healingActionSchema).optional(),
  expectedOutcome: z.string(),
  verificationWindowSeconds: numeric.optional(),
  llmTrace: llmTraceSchema,
});

export type HealingPlan = z.infer<typeof healingPlanSchema>;
