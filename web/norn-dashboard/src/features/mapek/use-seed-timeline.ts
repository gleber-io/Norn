import { useEffect } from "react";
import { useOutcomesQuery, usePlansQuery, useSignalsQuery } from "@/lib/queries";
import { laneFor, type TimelineEntry, useTimelineStore } from "@/stores/timeline-store";

/**
 * Reidrata a timeline a partir dos snapshots REST (ADR-15) — sem isto, uma reconexão perde os
 * eventos que aconteceram enquanto o SignalR esteve fora (o canal é best-effort, sem durabilidade).
 * `TopologyUpdated`/lane Analyze fica de fora: não existe recurso REST com histórico de contexto
 * correlacionado, só o estado atual de topologia — limitação aceita (ver docs/norn-api-contract.md).
 */
export function useSeedTimelineFromRest() {
  const signalsQuery = useSignalsQuery(200);
  const plansQuery = usePlansQuery(200);
  const outcomesQuery = useOutcomesQuery(200);
  const seedFromRest = useTimelineStore((s) => s.seedFromRest);

  useEffect(() => {
    if (!signalsQuery.data || !plansQuery.data || !outcomesQuery.data) {
      return;
    }

    const entries: TimelineEntry[] = [
      ...signalsQuery.data.map(
        (signal): TimelineEntry => ({
          id: `SignalDetected:${signal.signalId}`,
          lane: laneFor("SignalDetected"),
          eventType: "SignalDetected",
          correlationId: signal.signalId,
          occurredAtUtc: signal.detectedAtUtc,
          summary: `${signal.metricName} — ${signal.severity} (${signal.target.service})`,
        }),
      ),
      ...plansQuery.data.map(
        (plan): TimelineEntry => ({
          id: `PlanCreated:${plan.planId}`,
          lane: laneFor("PlanCreated"),
          eventType: "PlanCreated",
          correlationId: plan.contextId,
          occurredAtUtc: plan.createdAtUtc,
          summary: `${plan.actions?.[0]?.type ?? "NoOp"} — decidido por ${plan.decidedBy}`,
        }),
      ),
      ...outcomesQuery.data.flatMap((outcome): TimelineEntry[] => [
        {
          id: `ActionApplied:${outcome.outcomeId}`,
          lane: laneFor("ActionApplied"),
          eventType: "ActionApplied",
          correlationId: outcome.planId,
          occurredAtUtc: outcome.appliedAtUtc,
          summary: `${outcome.status}`,
        },
        {
          id: `OutcomeVerified:${outcome.outcomeId}`,
          lane: laneFor("OutcomeVerified"),
          eventType: "OutcomeVerified",
          correlationId: outcome.planId,
          occurredAtUtc: outcome.verifiedAtUtc,
          summary: `${outcome.status}${outcome.sloRestored ? " — SLO restaurado" : ""}`,
        },
      ]),
    ];

    // Roda de novo a cada refetch (inclusive nas reidratações de reconexão do useNornHub) — seguro
    // porque seedFromRest funde por id (Map), nunca substitui: eventos já chegados ao vivo não são
    // duplicados nem perdidos.
    seedFromRest(entries);
  }, [signalsQuery.data, plansQuery.data, outcomesQuery.data, seedFromRest]);
}
