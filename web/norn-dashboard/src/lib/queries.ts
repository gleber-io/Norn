import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getExperimentRun,
  getMetricsSeries,
  getMode,
  getOutcomes,
  getPlans,
  getSignals,
  getTopology,
  type PlatformMode,
  setMode,
} from "./api-client";
import { queryKeys } from "./query-client";

export const useTopologyQuery = () =>
  useQuery({ queryKey: queryKeys.topology, queryFn: getTopology });

export const useSignalsQuery = (limit = 50, experimentRunId?: string) =>
  useQuery({
    queryKey: queryKeys.signals(limit, experimentRunId),
    queryFn: () => getSignals({ limit, experimentRunId }),
  });

export const usePlansQuery = (limit = 50) =>
  useQuery({ queryKey: queryKeys.plans(limit), queryFn: () => getPlans({ limit }) });

export const useOutcomesQuery = (limit = 50) =>
  useQuery({ queryKey: queryKeys.outcomes(limit), queryFn: () => getOutcomes({ limit }) });

export const useModeQuery = () => useQuery({ queryKey: queryKeys.mode, queryFn: getMode });

export const useExperimentRunQuery = (runId: string | undefined) =>
  useQuery({
    queryKey: queryKeys.experimentRun(runId ?? ""),
    queryFn: () => getExperimentRun(runId as string),
    enabled: runId !== undefined,
  });

export const useMetricsSeriesQuery = (params: {
  metricName: string;
  service: string;
  fromUtc: string;
  toUtc: string;
}) =>
  useQuery({
    queryKey: queryKeys.metricsSeries(
      params.metricName,
      params.service,
      params.fromUtc,
      params.toUtc,
    ),
    queryFn: () => getMetricsSeries(params),
  });

/** PUT /mode nunca publica em `norn:events` (achado da Fase 10/11) — a UI só vê a troca de modo
 * confirmada de fato quando o `ModeChanged` do SignalR chegar (polling do Worker). Esta mutation
 * só reconcilia a query local `mode`; não assume sucesso "ao vivo" imediato. */
export const useSetModeMutation = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (mode: PlatformMode) => setMode(mode),
    onSuccess: (data) => {
      queryClient.setQueryData(queryKeys.mode, data);
    },
  });
};
