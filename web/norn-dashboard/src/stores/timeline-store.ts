import { create } from "zustand";

export type TimelineLane = "Monitor" | "Analyze" | "Plan" | "Execute";

export type TimelineEventType =
  | "SignalDetected"
  | "TopologyUpdated"
  | "PlanCreated"
  | "ActionApplied"
  | "OutcomeVerified";

export interface TimelineEntry {
  /** Único por linha — não é o id do domínio (outcome aparece 2x: ActionApplied e OutcomeVerified). */
  id: string;
  lane: TimelineLane;
  eventType: TimelineEventType;
  correlationId: string;
  occurredAtUtc: string;
  summary: string;
}

interface TimelineState {
  entries: TimelineEntry[];
  push: (entry: TimelineEntry) => void;
  seedFromRest: (entries: TimelineEntry[]) => void;
  clear: () => void;
}

const MAX_ENTRIES = 300;

function mergeSortedDesc(entries: TimelineEntry[]): TimelineEntry[] {
  const byId = new Map(entries.map((e) => [e.id, e]));
  return [...byId.values()]
    .sort((a, b) => b.occurredAtUtc.localeCompare(a.occurredAtUtc))
    .slice(0, MAX_ENTRIES);
}

export const useTimelineStore = create<TimelineState>((set) => ({
  entries: [],
  push: (entry) => set((state) => ({ entries: mergeSortedDesc([entry, ...state.entries]) })),
  seedFromRest: (entries) =>
    set((state) => ({ entries: mergeSortedDesc([...entries, ...state.entries]) })),
  clear: () => set({ entries: [] }),
}));

export function laneFor(eventType: TimelineEventType): TimelineLane {
  switch (eventType) {
    case "SignalDetected":
      return "Monitor";
    case "TopologyUpdated":
      return "Analyze";
    case "PlanCreated":
      return "Plan";
    case "ActionApplied":
    case "OutcomeVerified":
      return "Execute";
  }
}
