import { z } from "zod";
import { numeric, resourceSpecSchema } from "./common";

/** Payload do evento SignalR `TopologyUpdated` (§5.6) — objeto único, diferente da lista que
 * `GET /api/v1/topology` devolve (achado confirmado na exploração da Fase 11). */
export const topologyInfoSchema = z.object({
  service: z.string(),
  currentReplicas: numeric,
  desiredReplicas: numeric,
  resourceRequests: resourceSpecSchema,
  resourceLimits: resourceSpecSchema,
  dependsOn: z.array(z.string()).optional(),
  dependedOnBy: z.array(z.string()).optional(),
});

export type TopologyInfo = z.infer<typeof topologyInfoSchema>;
