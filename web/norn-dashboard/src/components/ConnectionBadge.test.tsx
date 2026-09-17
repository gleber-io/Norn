import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { useConnectionStore } from "@/stores/connection-store";
import { ConnectionBadge } from "./ConnectionBadge";

describe("ConnectionBadge", () => {
  afterEach(() => {
    useConnectionStore.setState({ status: "Connecting" });
  });

  it("mostra 'Conectado' quando o status é Connected", () => {
    useConnectionStore.setState({ status: "Connected" });
    render(<ConnectionBadge />);

    expect(screen.getByRole("status")).toHaveTextContent("Conectado");
  });

  it("mostra 'Desconectado' quando o status é Disconnected", () => {
    useConnectionStore.setState({ status: "Disconnected" });
    render(<ConnectionBadge />);

    expect(screen.getByRole("status")).toHaveTextContent("Desconectado");
  });

  it("mostra 'Reconectando…' quando o status é Reconnecting", () => {
    useConnectionStore.setState({ status: "Reconnecting" });
    render(<ConnectionBadge />);

    expect(screen.getByRole("status")).toHaveTextContent("Reconectando…");
  });
});
