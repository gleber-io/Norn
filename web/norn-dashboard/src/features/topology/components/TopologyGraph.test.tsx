import { screen } from "@testing-library/react";
import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/test-utils";
import { TopologyGraph } from "./TopologyGraph";

const CATALOG_TOPOLOGY = {
  service: "Norn.Shop.Catalog.API",
  currentReplicas: 1,
  desiredReplicas: 1,
  resourceRequests: { cpu: "100m", memory: "128Mi" },
  resourceLimits: { cpu: "500m", memory: "256Mi" },
  dependsOn: [],
  dependedOnBy: [],
};

describe("TopologyGraph", () => {
  it("mostra estado vazio quando não há serviço nenhum", async () => {
    renderWithProviders(<TopologyGraph />);

    expect(await screen.findByText("Nenhum serviço observado ainda.")).toBeInTheDocument();
  });

  it("renderiza um nó com aria-label descrevendo saúde e réplicas (base do foco por teclado do React Flow)", async () => {
    server.use(http.get("/api/v1/topology", () => HttpResponse.json([CATALOG_TOPOLOGY])));

    renderWithProviders(<TopologyGraph />);

    // aria-label vem do campo `ariaLabel` do Node (buildGraph) — é o que o React Flow usa pra
    // anunciar o nó a leitor de tela quando focado. O tabIndex/role de foco em si é interno ao
    // React Flow (nodesFocusable) e não observável de forma estável em jsdom nesta versão.
    expect(
      await screen.findByLabelText("Norn.Shop.Catalog.API, estado healthy, 1 de 1 réplicas"),
    ).toBeInTheDocument();
  });
});
