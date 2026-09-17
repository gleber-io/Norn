import { z } from "zod";
import {
  detectorTypeSchema,
  numeric,
  serviceTargetSchema,
  severitySchema,
  timeWindowSchema,
} from "./common";

/** Payload do evento SignalR `SignalDetected` (§5.6) — mesma forma de um item de `GET /api/v1/signals`. */
export const anomalySignalSchema = z.object({
  signalId: z.uuid(),
  detectedAtUtc: z.iso.datetime({ offset: true }),
  target: serviceTargetSchema,
  metricName: z.string(),
  detector: detectorTypeSchema,
  severity: severitySchema,
  confidence: numeric,
  pValue: numeric,
  observedValue: numeric,
  expectedValue: numeric,
  window: timeWindowSchema,
  experimentRunId: z.uuid().nullish(),
});

export type AnomalySignal = z.infer<typeof anomalySignalSchema>;
