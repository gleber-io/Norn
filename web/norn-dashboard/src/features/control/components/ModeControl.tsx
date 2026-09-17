import type { PlatformMode } from "@/lib/api-client";
import { useModeQuery, useSetModeMutation } from "@/lib/queries";
import { cn } from "@/lib/utils";

const MODES: PlatformMode[] = ["Observe", "DryRun", "Active"];

/** Seletor Observe/DryRun/Active (tarefa 9 da Fase 11). PUT /mode nunca publica em `norn:events`
 * (achado da Fase 10) — o valor mostrado aqui só é confirmado de fato quando `ModeChanged` chegar
 * pelo SignalR (polling do Worker), não como resposta imediata desta chamada; por isso o botão
 * fica com estado de "pendente" enquanto a mutation está em voo, mas o texto do modo atual só
 * muda quando a query `mode` for de fato atualizada (pela mutation ou pelo evento ao vivo). */
export function ModeControl() {
  const modeQuery = useModeQuery();
  const setMode = useSetModeMutation();

  const currentMode = modeQuery.data?.mode;

  const handleSelect = (mode: PlatformMode) => {
    if (mode === currentMode) {
      return;
    }
    if (mode === "Active") {
      const confirmed = window.confirm(
        "Mudar pro modo Active permite que o Norn aplique ações de cura de verdade no cluster. Confirmar?",
      );
      if (!confirmed) {
        return;
      }
    }
    setMode.mutate(mode);
  };

  return (
    <fieldset className="inline-flex items-center gap-1 rounded-md border border-border p-1">
      <legend className="sr-only">Modo da plataforma</legend>
      {MODES.map((mode) => (
        <button
          key={mode}
          type="button"
          disabled={setMode.isPending}
          aria-pressed={mode === currentMode}
          onClick={() => handleSelect(mode)}
          className={cn(
            "rounded px-3 py-1 text-sm transition-colors",
            mode === currentMode
              ? "bg-primary text-primary-foreground"
              : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
            setMode.isPending && "opacity-50",
          )}
        >
          {mode}
        </button>
      ))}
    </fieldset>
  );
}
