import * as signalR from "@microsoft/signalr";
import type { QueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import type { z } from "zod";
import { useConnectionStore } from "@/stores/connection-store";
import type { TimelineEntry } from "@/stores/timeline-store";
import { laneFor, useTimelineStore } from "@/stores/timeline-store";
import { queryKeys } from "./query-client";
import type { AnomalySignal } from "./schemas/anomaly-signal";
import { anomalySignalSchema } from "./schemas/anomaly-signal";
import { platformModeSchema } from "./schemas/common";
import type { HealingOutcome } from "./schemas/healing-outcome";
import { healingOutcomeSchema } from "./schemas/healing-outcome";
import type { HealingPlan } from "./schemas/healing-plan";
import { healingPlanSchema } from "./schemas/healing-plan";
import { topologyInfoSchema } from "./schemas/topology-info";

const HUB_URL = `${import.meta.env.VITE_API_BASE_URL ?? ""}/hubs/norn`;

// Prefixos, não as chaves inteiras: `signals`/`plans`/`outcomes` levam `limit` na chave completa
// (queryKeys.ts), e o prefixo é o que casa com todas as variantes de limite ativas ao mesmo tempo
// — tanto pra invalidar (rehydrate) quanto pra aplicar um evento ao vivo (setQueriesData abaixo).
const REST_QUERY_KEY_PREFIXES = [["topology"], ["signals"], ["plans"], ["outcomes"], ["mode"]];

/** Registra um evento do hub validado pelo Zod correspondente antes de repassar ao `handler` —
 * payload inválido é logado e descartado, nunca derruba a conexão (mesma postura do
 * PlatformEventRelay do lado do servidor). Elimina a repetição de `safeParse`/`console.warn` que
 * os seis eventos teriam se registrados um a um. */
function registerValidatedEvent<T>(
  connection: signalR.HubConnection,
  eventName: string,
  schema: z.ZodType<T>,
  handler: (data: T) => void,
) {
  connection.on(eventName, (raw: unknown) => {
    const parsed = schema.safeParse(raw);
    if (!parsed.success) {
      console.warn(`${eventName}: payload inválido, descartado`, parsed.error);
      return;
    }
    handler(parsed.data);
  });
}

/** Conecta ao NornHub, reidrata por REST a cada conexão/reconexão (ADR-15 — o canal é best-effort,
 * sem durabilidade). */
export function useNornHub(queryClient: QueryClient) {
  const setStatus = useConnectionStore((s) => s.setStatus);
  const push = useTimelineStore((s) => s.push);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 15000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    const rehydrate = () => {
      for (const queryKey of REST_QUERY_KEY_PREFIXES) {
        queryClient.invalidateQueries({ queryKey });
      }
    };

    connection.onreconnecting(() => setStatus("Reconnecting"));
    connection.onreconnected(() => {
      setStatus("Connected");
      rehydrate();
    });
    connection.onclose(() => setStatus("Disconnected"));

    registerValidatedEvent(connection, "SignalDetected", anomalySignalSchema, (signal) =>
      onSignalDetected(queryClient, push, signal),
    );

    registerValidatedEvent(connection, "TopologyUpdated", topologyInfoSchema, (topology) => {
      queryClient.setQueryData(queryKeys.topology, (old: (typeof topology)[] | undefined) => {
        const rest = (old ?? []).filter((t) => t.service !== topology.service);
        return [...rest, topology];
      });
      push({
        id: `TopologyUpdated:${topology.service}:${Date.now()}`,
        lane: laneFor("TopologyUpdated"),
        eventType: "TopologyUpdated",
        correlationId: "",
        occurredAtUtc: new Date().toISOString(),
        summary: `Contexto correlacionado — ${topology.service}`,
      });
    });

    registerValidatedEvent(connection, "PlanCreated", healingPlanSchema, (plan) =>
      onPlanCreated(queryClient, push, plan),
    );

    registerValidatedEvent(connection, "ActionApplied", healingOutcomeSchema, (outcome) =>
      onOutcomeEvent(queryClient, push, "ActionApplied", outcome),
    );

    registerValidatedEvent(connection, "OutcomeVerified", healingOutcomeSchema, (outcome) =>
      onOutcomeEvent(queryClient, push, "OutcomeVerified", outcome),
    );

    registerValidatedEvent(connection, "ModeChanged", platformModeSchema, (mode) =>
      queryClient.setQueryData(queryKeys.mode, { mode }),
    );

    setStatus("Connecting");
    connection
      .start()
      .then(() => {
        setStatus("Connected");
        rehydrate();
      })
      .catch((error) => {
        console.error("Falha ao conectar no NornHub", error);
        setStatus("Disconnected");
      });

    return () => {
      void connection.stop();
    };
  }, [queryClient, setStatus, push]);
}

function onSignalDetected(
  queryClient: QueryClient,
  push: (entry: TimelineEntry) => void,
  signal: AnomalySignal,
) {
  queryClient.setQueriesData<AnomalySignal[]>({ queryKey: ["signals"] }, (old) => {
    if (old?.some((s) => s.signalId === signal.signalId)) {
      return old;
    }
    return [signal, ...(old ?? [])];
  });
  push({
    id: `SignalDetected:${signal.signalId}`,
    lane: laneFor("SignalDetected"),
    eventType: "SignalDetected",
    correlationId: signal.signalId,
    occurredAtUtc: signal.detectedAtUtc,
    summary: `${signal.metricName} — ${signal.severity} (${signal.target.service})`,
  });
}

function onPlanCreated(
  queryClient: QueryClient,
  push: (entry: TimelineEntry) => void,
  plan: HealingPlan,
) {
  queryClient.setQueriesData<HealingPlan[]>({ queryKey: ["plans"] }, (old) => {
    if (old?.some((p) => p.planId === plan.planId)) {
      return old;
    }
    return [plan, ...(old ?? [])];
  });
  const actionSummary = plan.actions?.[0]?.type ?? "NoOp";
  push({
    id: `PlanCreated:${plan.planId}`,
    lane: laneFor("PlanCreated"),
    eventType: "PlanCreated",
    correlationId: plan.contextId,
    occurredAtUtc: plan.createdAtUtc,
    summary: `${actionSummary} — decidido por ${plan.decidedBy}`,
  });
}

function onOutcomeEvent(
  queryClient: QueryClient,
  push: (entry: TimelineEntry) => void,
  eventType: "ActionApplied" | "OutcomeVerified",
  outcome: HealingOutcome,
) {
  queryClient.setQueriesData<HealingOutcome[]>({ queryKey: ["outcomes"] }, (old) => {
    const rest = (old ?? []).filter((o) => o.outcomeId !== outcome.outcomeId);
    return [outcome, ...rest];
  });
  push({
    id: `${eventType}:${outcome.outcomeId}`,
    lane: laneFor(eventType),
    eventType,
    correlationId: outcome.planId,
    occurredAtUtc: eventType === "ActionApplied" ? outcome.appliedAtUtc : outcome.verifiedAtUtc,
    summary: `${outcome.status}${outcome.sloRestored ? " — SLO restaurado" : ""}`,
  });
}
