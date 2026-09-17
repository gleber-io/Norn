import { z } from "zod";
import { numeric, serviceTargetSchema } from "./common";

/** Item de `GET /api/v1/metrics/series` (tarefa 8 da Fase 11). */
export const metricSampleSchema = z.object({
  metricName: z.string(),
  target: serviceTargetSchema,
  timestampUtc: z.iso.datetime({ offset: true }),
  value: numeric,
  labels: z.record(z.string(), z.string()).optional(),
});

export type MetricSample = z.infer<typeof metricSampleSchema>;
