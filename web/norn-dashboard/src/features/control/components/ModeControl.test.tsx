import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { renderWithProviders } from "@/test/test-utils";
import { ModeControl } from "./ModeControl";

describe("ModeControl", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("mostra os três modos e marca o atual (Observe, do handler MSW padrão) como pressionado", async () => {
    renderWithProviders(<ModeControl />);

    const observeButton = await screen.findByRole("button", { name: "Observe" });
    await waitFor(() => expect(observeButton).toHaveAttribute("aria-pressed", "true"));
    expect(screen.getByRole("button", { name: "DryRun" })).toHaveAttribute("aria-pressed", "false");
  });

  it("pede confirmação explícita antes de mudar pra Active, e não muda se o usuário cancelar", async () => {
    const user = userEvent.setup();
    vi.spyOn(window, "confirm").mockReturnValue(false);
    renderWithProviders(<ModeControl />);
    await screen.findByRole("button", { name: "Observe" });

    await user.click(screen.getByRole("button", { name: "Active" }));

    expect(window.confirm).toHaveBeenCalledOnce();
    expect(screen.getByRole("button", { name: "Active" })).toHaveAttribute("aria-pressed", "false");
  });

  it("muda pra Active quando o usuário confirma", async () => {
    const user = userEvent.setup();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    renderWithProviders(<ModeControl />);
    await screen.findByRole("button", { name: "Observe" });

    await user.click(screen.getByRole("button", { name: "Active" }));

    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Active" })).toHaveAttribute(
        "aria-pressed",
        "true",
      ),
    );
  });

  it("nunca pede confirmação pra Observe/DryRun", async () => {
    const user = userEvent.setup();
    vi.spyOn(window, "confirm");
    renderWithProviders(<ModeControl />);
    await screen.findByRole("button", { name: "Observe" });

    await user.click(screen.getByRole("button", { name: "DryRun" }));

    expect(window.confirm).not.toHaveBeenCalled();
  });
});
