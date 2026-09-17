import { cn } from "@/lib/utils";
import { type TimelineEntry, type TimelineLane, useTimelineStore } from "@/stores/timeline-store";

const LANES: TimelineLane[] = ["Monitor", "Analyze", "Plan", "Execute"];

const LANE_CLASS: Record<TimelineLane, string> = {
  Monitor: "border-t-blue-500",
  Analyze: "border-t-amber-500",
  Plan: "border-t-purple-500",
  Execute: "border-t-status-healthy",
};

/** Timeline MAPE-K em 4 faixas (tarefa 6 da Fase 11) — vocabulário do MAPE-K sem sinônimo (D1).
 * `aria-live="polite"` anuncia cada evento novo pra quem usa leitor de tela; é o item de
 * acessibilidade que nenhum linter cobre (§7.3), só Lighthouse + inspeção manual. */
export function Timeline() {
  const entries = useTimelineStore((s) => s.entries);
  const latest = entries[0];

  return (
    <div>
      <span className="sr-only" role="status" aria-live="polite">
        {latest ? `${latest.lane}: ${latest.summary}` : ""}
      </span>
      <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
        {LANES.map((lane) => (
          <TimelineLaneColumn
            key={lane}
            lane={lane}
            entries={entries.filter((e) => e.lane === lane)}
          />
        ))}
      </div>
    </div>
  );
}

function TimelineLaneColumn({ lane, entries }: { lane: TimelineLane; entries: TimelineEntry[] }) {
  return (
    <div className={cn("rounded-lg border border-border border-t-4", LANE_CLASS[lane])}>
      <h3 className="border-b border-border px-3 py-2 text-sm font-semibold">{lane}</h3>
      <ol className="flex max-h-80 flex-col gap-1 overflow-y-auto p-2">
        {entries.length === 0 && (
          <li className="px-1 py-2 text-xs text-muted-foreground">Sem eventos ainda.</li>
        )}
        {entries.map((entry) => (
          <li key={entry.id} className="rounded bg-muted px-2 py-1.5 text-xs">
            <div className="text-muted-foreground">
              {new Date(entry.occurredAtUtc).toLocaleTimeString("pt-BR")}
            </div>
            <div>{entry.summary}</div>
          </li>
        ))}
      </ol>
    </div>
  );
}
