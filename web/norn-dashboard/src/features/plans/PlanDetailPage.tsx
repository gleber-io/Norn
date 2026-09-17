import { useParams } from "react-router";
import { QueryState } from "@/components/query-state";
import type { HealingOutcome } from "@/lib/api-client";
import { useOutcomesQuery, usePlansQuery } from "@/lib/queries";

type NumericMetricKey = Exclude<keyof HealingOutcome["metricsBefore"], "errorsByType">;

const METRIC_LABELS: { key: NumericMetricKey; label: string }[] = [
  { key: "cpuUtilizationPct", label: "CPU (%)" },
  { key: "memoryWorkingSetBytes", label: "Memória (bytes)" },
  { key: "requestRatePerSecond", label: "Requisições/s" },
  { key: "errorRatePct", label: "Taxa de erro (%)" },
  { key: "latencyP99Ms", label: "Latência p99 (ms)" },
  { key: "queueDepth", label: "Profundidade de fila" },
];

export function PlanDetailPage() {
  const { planId } = useParams<{ planId: string }>();
  const plansQuery = usePlansQuery(200);
  const outcomesQuery = useOutcomesQuery(200);

  const plan = plansQuery.data?.find((p) => p.planId === planId);
  const outcome = outcomesQuery.data?.find((o) => o.planId === planId);

  return (
    <QueryState
      isLoading={plansQuery.isLoading}
      isError={plansQuery.isError}
      error={plansQuery.error}
      isEmpty={plan === undefined}
      emptyMessage="Plano não encontrado (ainda não chegou pelo REST/SignalR)."
    >
      {plan && (
        <div className="flex flex-col gap-6">
          <section>
            <h2 className="text-lg font-semibold">Rationale</h2>
            <p className="mt-1 text-muted-foreground">{plan.rationale}</p>
            <dl className="mt-3 grid grid-cols-2 gap-2 text-sm sm:grid-cols-4">
              <div>
                <dt className="text-muted-foreground">Decidido por</dt>
                <dd>{plan.decidedBy}</dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Confiança</dt>
                <dd>{plan.confidence.toFixed(1)}%</dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Modelo</dt>
                <dd>{plan.llmTrace.model ?? "—"}</dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Latência do LLM</dt>
                <dd>{plan.llmTrace.latencyMs ? `${plan.llmTrace.latencyMs}ms` : "—"}</dd>
              </div>
            </dl>
          </section>

          <section>
            <h2 className="text-lg font-semibold">Ações</h2>
            <ul className="mt-2 flex flex-col gap-2">
              {(plan.actions ?? []).map((action) => (
                <li key={action.actionId} className="rounded-md border border-border p-3 text-sm">
                  <div className="font-medium">
                    {action.type} — {action.target.service}
                  </div>
                  {action.parameters && Object.keys(action.parameters).length > 0 && (
                    <dl className="mt-1 flex flex-wrap gap-x-4 text-muted-foreground">
                      {Object.entries(action.parameters).map(([key, value]) => (
                        <div key={key} className="flex gap-1">
                          <dt>{key}:</dt>
                          <dd>{value}</dd>
                        </div>
                      ))}
                    </dl>
                  )}
                </li>
              ))}
              {(plan.actions ?? []).length === 0 && (
                <li className="text-sm text-muted-foreground">NoOp.</li>
              )}
            </ul>
          </section>

          {outcome && (
            <section>
              <h2 className="text-lg font-semibold">
                Resultado — {outcome.status}
                {outcome.sloRestored && " (SLO restaurado)"}
              </h2>
              <table className="mt-2 w-full text-sm">
                <thead>
                  <tr className="border-b border-border text-left text-muted-foreground">
                    <th className="py-1 font-normal">Métrica</th>
                    <th className="py-1 font-normal">Antes</th>
                    <th className="py-1 font-normal">Depois</th>
                  </tr>
                </thead>
                <tbody>
                  {METRIC_LABELS.map(({ key, label }) => (
                    <tr key={key} className="border-b border-border last:border-0">
                      <td className="py-1">{label}</td>
                      <td className="py-1">{outcome.metricsBefore[key].toString()}</td>
                      <td className="py-1">{outcome.metricsAfter[key].toString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          )}
        </div>
      )}
    </QueryState>
  );
}
