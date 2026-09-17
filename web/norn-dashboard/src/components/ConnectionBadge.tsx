import { cn } from "@/lib/utils";
import { useConnectionStore } from "@/stores/connection-store";

const LABEL: Record<string, string> = {
  Connected: "Conectado",
  Connecting: "Conectando…",
  Reconnecting: "Reconectando…",
  Disconnected: "Desconectado",
};

const DOT_CLASS: Record<string, string> = {
  Connected: "bg-status-healthy",
  Connecting: "bg-status-degraded",
  Reconnecting: "bg-status-degraded animate-pulse",
  Disconnected: "bg-status-critical",
};

export function ConnectionBadge() {
  const status = useConnectionStore((s) => s.status);

  return (
    <span
      className="inline-flex items-center gap-2 rounded-full border border-border px-3 py-1 text-sm"
      role="status"
      aria-live="polite"
    >
      <span className={cn("size-2 rounded-full", DOT_CLASS[status])} aria-hidden="true" />
      {LABEL[status]}
    </span>
  );
}
