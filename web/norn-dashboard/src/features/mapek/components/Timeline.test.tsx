import { render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { useTimelineStore } from "@/stores/timeline-store";
import { Timeline } from "./Timeline";

describe("Timeline", () => {
  afterEach(() => {
    useTimelineStore.setState({ entries: [] });
  });

  it("mostra as quatro faixas do MAPE-K mesmo sem eventos", () => {
    render(<Timeline />);

    expect(screen.getByRole("heading", { name: "Monitor" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Analyze" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Plan" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Execute" })).toBeInTheDocument();
  });

  it("coloca um evento SignalDetected na faixa Monitor", () => {
    useTimelineStore.getState().push({
      id: "SignalDetected:1",
      lane: "Monitor",
      eventType: "SignalDetected",
      correlationId: "sig-1",
      occurredAtUtc: new Date().toISOString(),
      summary: "dotnet_process_memory_working_set_bytes — Critical (Catalog.API)",
    });

    render(<Timeline />);

    const monitorHeading = screen.getByRole("heading", { name: "Monitor" });
    const monitorColumn = monitorHeading.closest("div") as HTMLElement;
    expect(within(monitorColumn).getByText(/Critical \(Catalog\.API\)/)).toBeInTheDocument();
  });

  it("anuncia o evento mais recente numa região aria-live", () => {
    useTimelineStore.getState().push({
      id: "PlanCreated:1",
      lane: "Plan",
      eventType: "PlanCreated",
      correlationId: "ctx-1",
      occurredAtUtc: new Date().toISOString(),
      summary: "ScaleUp — decidido por Llm",
    });

    render(<Timeline />);

    const liveRegion = screen.getByRole("status", { hidden: true });
    expect(liveRegion).toHaveAttribute("aria-live", "polite");
    expect(liveRegion).toHaveTextContent("Plan: ScaleUp — decidido por Llm");
  });
});
