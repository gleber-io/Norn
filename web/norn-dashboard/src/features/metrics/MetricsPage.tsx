import { useMemo, useState } from "react";
import {
  CartesianGrid,
  Line,
  LineChart,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { QueryState } from "@/components/query-state";
import {
  useMetricsSeriesQuery,
  useOutcomesQuery,
  useSignalsQuery,
  useTopologyQuery,
} from "@/lib/queries";
import { METRIC_CATALOG } from "./metric-catalog";

const WINDOW_MINUTES = 15;

/** Feature `metrics` (tarefa 8 da Fase 11) — série real de `GET /api/v1/metrics/series`, com o
 * instante do sinal/ação marcado no cliente (o endpoint devolve só a série; ver
 * docs/norn-api-contract.md §5 pro motivo). */
export function MetricsPage() {
  const topologyQuery = useTopologyQuery();
  const services = topologyQuery.data?.map((t) => t.service) ?? [];

  const [service, setService] = useState<string>("");
  const [metricName, setMetricName] = useState<string>(METRIC_CATALOG[0]);

  const effectiveService = service || services[0] || "";

  // Janela fixa por montagem do componente, não recalculada a cada render — troca de
  // métrica/serviço já refaz a query pela mudança de queryKey, sem precisar de uma janela nova.
  const { fromUtc, toUtc } = useMemo(() => {
    const to = new Date();
    const from = new Date(to.getTime() - WINDOW_MINUTES * 60_000);
    return { fromUtc: from.toISOString(), toUtc: to.toISOString() };
  }, []);

  const seriesQuery = useMetricsSeriesQuery({
    metricName,
    service: effectiveService,
    fromUtc,
    toUtc,
  });
  const signalsQuery = useSignalsQuery(200);
  const outcomesQuery = useOutcomesQuery(200);

  const chartData = (seriesQuery.data ?? []).map((point) => ({
    timestamp: new Date(point.timestampUtc).getTime(),
    value: point.value,
  }));

  const signalMarkers = (signalsQuery.data ?? [])
    .filter((s) => s.target.service === effectiveService && s.metricName === metricName)
    .map((s) => ({ timestamp: new Date(s.detectedAtUtc).getTime(), label: "sinal" }));

  const actionMarkers = (outcomesQuery.data ?? [])
    .filter((o) => o.appliedAtUtc >= fromUtc && o.appliedAtUtc <= toUtc)
    .map((o) => ({ timestamp: new Date(o.appliedAtUtc).getTime(), label: "ação" }));

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-4">
        <label className="flex flex-col gap-1 text-sm">
          Serviço
          <select
            className="rounded-md border border-border bg-background px-2 py-1"
            value={effectiveService}
            onChange={(e) => setService(e.target.value)}
          >
            {services.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          Métrica
          <select
            className="rounded-md border border-border bg-background px-2 py-1"
            value={metricName}
            onChange={(e) => setMetricName(e.target.value)}
          >
            {METRIC_CATALOG.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </select>
        </label>
      </div>

      <QueryState
        isLoading={seriesQuery.isLoading}
        isError={seriesQuery.isError}
        error={seriesQuery.error}
        isEmpty={chartData.length === 0}
        emptyMessage={`Sem pontos pra ${metricName} em ${effectiveService} nos últimos ${WINDOW_MINUTES} min.`}
      >
        <div className="h-96 rounded-lg border border-border p-4">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis
                dataKey="timestamp"
                type="number"
                domain={["dataMin", "dataMax"]}
                tickFormatter={(t) => new Date(t).toLocaleTimeString("pt-BR")}
              />
              <YAxis />
              <Tooltip labelFormatter={(t) => new Date(t as number).toLocaleTimeString("pt-BR")} />
              <Line
                type="monotone"
                dataKey="value"
                stroke="var(--color-primary)"
                dot={false}
                isAnimationActive={false}
              />
              {signalMarkers.map((m) => (
                <ReferenceLine
                  key={`signal-${m.timestamp}`}
                  x={m.timestamp}
                  stroke="var(--color-status-critical)"
                  strokeDasharray="4 4"
                  label={{ value: "sinal", position: "top", fontSize: 10 }}
                />
              ))}
              {actionMarkers.map((m) => (
                <ReferenceLine
                  key={`action-${m.timestamp}`}
                  x={m.timestamp}
                  stroke="var(--color-status-healthy)"
                  strokeDasharray="4 4"
                  label={{ value: "ação", position: "top", fontSize: 10 }}
                />
              ))}
            </LineChart>
          </ResponsiveContainer>
        </div>
      </QueryState>
    </div>
  );
}
