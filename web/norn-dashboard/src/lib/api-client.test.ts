import { HttpResponse, http } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "@/test/msw/server";
import { getMode, getTopology, setMode } from "./api-client";

describe("api-client", () => {
  it("getTopology normaliza currentReplicas/desiredReplicas de string pra number", async () => {
    server.use(
      http.get("/api/v1/topology", () =>
        HttpResponse.json([
          {
            service: "Norn.Shop.Catalog.API",
            currentReplicas: "2",
            desiredReplicas: "3",
            resourceRequests: { cpu: "100m", memory: "128Mi" },
            resourceLimits: { cpu: "500m", memory: "256Mi" },
          },
        ]),
      ),
    );

    const topology = await getTopology();

    expect(topology[0]?.currentReplicas).toBe(2);
    expect(typeof topology[0]?.currentReplicas).toBe("number");
  });

  it("getMode devolve o modo do handler MSW padrão (Observe)", async () => {
    const mode = await getMode();
    expect(mode.mode).toBe("Observe");
  });

  it("setMode lança ApiError quando a Norn.API responde erro", async () => {
    server.use(
      http.put("/api/v1/mode", () => HttpResponse.json({ title: "erro" }, { status: 400 })),
    );

    await expect(setMode("Active")).rejects.toThrow(/Falha na chamada à Norn\.API/);
  });
});
