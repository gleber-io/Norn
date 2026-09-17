import { HttpResponse, http } from "msw";

/** Handlers padrão dos 8 endpoints REST da Norn.API — testes de componente sobrescrevem com
 * `server.use(...)` quando precisam de um cenário específico (nunca batem na Norn.API real). */
export const handlers = [
  http.get("/api/v1/topology", () => HttpResponse.json([])),
  http.get("/api/v1/signals", () => HttpResponse.json([])),
  http.get("/api/v1/plans", () => HttpResponse.json([])),
  http.get("/api/v1/outcomes", () => HttpResponse.json([])),
  http.get("/api/v1/mode", () => HttpResponse.json({ mode: "Observe" })),
  http.put("/api/v1/mode", async ({ request }) => HttpResponse.json(await request.json())),
  http.get("/api/v1/experiments/:runId", () => HttpResponse.json(null, { status: 404 })),
  http.get("/api/v1/metrics/series", () => HttpResponse.json([])),
];
