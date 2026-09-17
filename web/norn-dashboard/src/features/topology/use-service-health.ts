import { useMemo } from "react";
import type { AnomalySignal, HealingOutcome } from "@/lib/api-client";
import { useOutcomesQuery, usePlansQuery, useSignalsQuery } from "@/lib/queries";

export type ServiceHealth = "healthy" | "degraded" | "critical";

/**
 * Deriva a saúde de cada serviço a partir do sinal mais recente que o mira e do outcome mais
 * recente de um plano cuja ação mira o mesmo serviço (`HealingOutcome` não carrega `ServiceTarget`
 * direto — só por meio de `HealingPlan.actions[].target`). Sem essa reconstrução não dá pra saber
 * se uma cura já resolveu o sinal mais recente — é o que faz o nó voltar de vermelho pra verde no
 * roteiro do DoD da Fase 11.
 */
export function useServiceHealth(): { health: Map<string, ServiceHealth>; isLoading: boolean } {
  const signalsQuery = useSignalsQuery(200);
  const plansQuery = usePlansQuery(200);
  const outcomesQuery = useOutcomesQuery(200);

  const health = useMemo(() => {
    const signals = signalsQuery.data ?? [];
    const plans = plansQuery.data ?? [];
    const outcomes = outcomesQuery.data ?? [];

    const planById = new Map(plans.map((p) => [p.planId, p]));

    const latestSignalByService = new Map<string, AnomalySignal>();
    for (const signal of signals) {
      const service = signal.target.service;
      const current = latestSignalByService.get(service);
      if (!current || signal.detectedAtUtc > current.detectedAtUtc) {
        latestSignalByService.set(service, signal);
      }
    }

    // Suposição: o plano de cada outcome está dentro da mesma janela de `limit=200` buscada acima.
    // Se um outcome estiver nos 200 mais recentes mas o plano correspondente já tiver saído da
    // janela (improvável com o volume desta fase, mas possível sob carga alta), `plan` vem
    // `undefined` e o outcome silenciosamente não contribui pra saúde de nenhum serviço — sem
    // sinal de degradação de dado. Aceito por ora; revisar se o limite virar gargalo real.
    const latestOutcomeByService = new Map<string, HealingOutcome>();
    for (const outcome of outcomes) {
      const plan = planById.get(outcome.planId);
      const services = new Set((plan?.actions ?? []).map((a) => a.target.service));
      for (const service of services) {
        const current = latestOutcomeByService.get(service);
        if (!current || outcome.verifiedAtUtc > current.verifiedAtUtc) {
          latestOutcomeByService.set(service, outcome);
        }
      }
    }

    const result = new Map<string, ServiceHealth>();
    const services = new Set([...latestSignalByService.keys(), ...latestOutcomeByService.keys()]);
    for (const service of services) {
      result.set(
        service,
        deriveHealth(latestSignalByService.get(service), latestOutcomeByService.get(service)),
      );
    }
    return result;
  }, [signalsQuery.data, plansQuery.data, outcomesQuery.data]);

  return {
    health,
    isLoading: signalsQuery.isLoading || plansQuery.isLoading || outcomesQuery.isLoading,
  };
}

function deriveHealth(
  signal: AnomalySignal | undefined,
  outcome: HealingOutcome | undefined,
): ServiceHealth {
  if (!signal) {
    return "healthy";
  }
  const resolvedAfterSignal = outcome?.sloRestored && outcome.verifiedAtUtc > signal.detectedAtUtc;
  if (resolvedAfterSignal) {
    return "healthy";
  }
  if (signal.severity === "Critical" || signal.severity === "High") {
    return "critical";
  }
  if (signal.severity === "Medium") {
    return "degraded";
  }
  return "healthy";
}
