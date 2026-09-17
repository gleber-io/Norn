import { QueryClient } from "@tanstack/react-query";

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 10_000,
      refetchOnWindowFocus: false,
    },
  },
});

// `signals`/`plans`/`outcomes` levam `limit` na chave — sem isso, duas telas usando limites
// diferentes (ex. lista com 100, detalhe com 200) disputariam a mesma entrada de cache, e qual
// limite "vence" passaria a depender da ordem de montagem dos componentes, não de quem está lendo
// (achado real do code-reviewer antes do commit deste bloco). `signalr.ts` invalida/atualiza por
// prefixo (`["signals"]`, sem o limit) pra alcançar todas as variantes ativas de uma vez.
export const queryKeys = {
  topology: ["topology"] as const,
  signals: (limit: number, experimentRunId?: string) =>
    ["signals", limit, experimentRunId] as const,
  plans: (limit: number) => ["plans", limit] as const,
  outcomes: (limit: number) => ["outcomes", limit] as const,
  mode: ["mode"] as const,
  experimentRun: (runId: string) => ["experiments", runId] as const,
  metricsSeries: (metricName: string, service: string, fromUtc: string, toUtc: string) =>
    ["metrics-series", metricName, service, fromUtc, toUtc] as const,
};
