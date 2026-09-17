import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { type ReactNode, useState } from "react";
import { describe, expect, it } from "vitest";
import { server } from "@/test/msw/server";
import { useServiceHealth } from "./use-service-health";

const SERVICE = "Norn.Shop.Catalog.API";

// Nome capitalizado (não `wrapper`) — é o que o Biome exige pra reconhecer isto como componente
// React e aceitar o `useState` dentro. `useState` (não `new QueryClient()` direto no corpo): este
// componente reexecuta a cada re-render do teste; sem lazy init, cada re-render trocaria o
// QueryClient inteiro e as queries nunca sairiam de "loading" (achado real ao escrever este teste).
function Wrapper({ children }: { children: ReactNode }) {
  const [queryClient] = useState(
    () => new QueryClient({ defaultOptions: { queries: { retry: false } } }),
  );
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

const target = { service: SERVICE, namespace: "norn-shop" };

describe("useServiceHealth", () => {
  it("marca como healthy um serviço sem sinal nenhum", async () => {
    const { result } = renderHook(() => useServiceHealth(), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.health.get(SERVICE)).toBeUndefined();
  });

  it("marca como critical um serviço com sinal Critical sem outcome depois", async () => {
    server.use(
      http.get("/api/v1/signals", () =>
        HttpResponse.json([
          {
            signalId: "11111111-1111-4111-8111-111111111111",
            detectedAtUtc: "2026-01-01T00:00:00Z",
            target,
            metricName: "dotnet_process_memory_working_set_bytes",
            detector: "SpikeDetection",
            severity: "Critical",
            confidence: 95,
            pValue: 0.001,
            observedValue: 500000000,
            expectedValue: 200000000,
            window: { fromUtc: "2026-01-01T00:00:00Z", toUtc: "2026-01-01T00:01:00Z" },
          },
        ]),
      ),
    );

    const { result } = renderHook(() => useServiceHealth(), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.health.get(SERVICE)).toBe("critical"));
  });

  it("volta pra healthy quando um outcome com sloRestored acontece depois do sinal", async () => {
    server.use(
      http.get("/api/v1/signals", () =>
        HttpResponse.json([
          {
            signalId: "11111111-1111-4111-8111-111111111111",
            detectedAtUtc: "2026-01-01T00:00:00Z",
            target,
            metricName: "dotnet_process_memory_working_set_bytes",
            detector: "SpikeDetection",
            severity: "Critical",
            confidence: 95,
            pValue: 0.001,
            observedValue: 500000000,
            expectedValue: 200000000,
            window: { fromUtc: "2026-01-01T00:00:00Z", toUtc: "2026-01-01T00:01:00Z" },
          },
        ]),
      ),
      http.get("/api/v1/plans", () =>
        HttpResponse.json([
          {
            planId: "22222222-2222-4222-8222-222222222222",
            contextId: "33333333-3333-4333-8333-333333333333",
            createdAtUtc: "2026-01-01T00:00:30Z",
            decidedBy: "RuleEngine",
            rationale: "RSS acima do limiar",
            confidence: 90,
            actions: [
              {
                actionId: "44444444-4444-4444-8444-444444444444",
                type: "RestartPod",
                target,
                order: 1,
              },
            ],
            expectedOutcome: "RSS volta ao normal",
            llmTrace: {},
          },
        ]),
      ),
      http.get("/api/v1/outcomes", () =>
        HttpResponse.json([
          {
            outcomeId: "55555555-5555-4555-8555-555555555555",
            planId: "22222222-2222-4222-8222-222222222222",
            appliedAtUtc: "2026-01-01T00:00:40Z",
            verifiedAtUtc: "2026-01-01T00:02:00Z",
            status: "Succeeded",
            sloRestored: true,
            timeToRecoverySeconds: 90,
            metricsBefore: {
              cpuUtilizationPct: 10,
              memoryWorkingSetBytes: 500000000,
              requestRatePerSecond: 5,
              errorRatePct: 0,
              latencyP99Ms: 50,
              queueDepth: 0,
            },
            metricsAfter: {
              cpuUtilizationPct: 8,
              memoryWorkingSetBytes: 120000000,
              requestRatePerSecond: 5,
              errorRatePct: 0,
              latencyP99Ms: 40,
              queueDepth: 0,
            },
          },
        ]),
      ),
    );

    const { result } = renderHook(() => useServiceHealth(), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.health.get(SERVICE)).toBe("healthy"));
  });
});
