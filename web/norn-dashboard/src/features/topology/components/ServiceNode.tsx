import { Handle, type NodeProps, Position } from "@xyflow/react";
import { cn } from "@/lib/utils";
import type { ServiceHealth } from "../use-service-health";

export interface ServiceNodeData extends Record<string, unknown> {
  label: string;
  currentReplicas: number;
  desiredReplicas: number;
  health: ServiceHealth;
}

const HEALTH_CLASS: Record<ServiceHealth, string> = {
  healthy: "border-status-healthy bg-status-healthy/10",
  degraded: "border-status-degraded bg-status-degraded/10",
  critical: "border-status-critical bg-status-critical/10",
};

const HEALTH_LABEL: Record<ServiceHealth, string> = {
  healthy: "saudável",
  degraded: "degradado",
  critical: "crítico",
};

// Foco por teclado vem do próprio React Flow (nodesFocusable, tabIndex/role na wrapper do nó) — o
// aria-label é passado no objeto Node (ver buildGraph em TopologyGraph.tsx), não aqui.
export function ServiceNode({ data }: NodeProps & { data: ServiceNodeData }) {
  return (
    <div
      className={cn("rounded-lg border-2 px-4 py-3 text-sm shadow-sm", HEALTH_CLASS[data.health])}
    >
      <Handle type="target" position={Position.Left} />
      <div className="font-medium">{data.label}</div>
      <div className="text-muted-foreground">
        {data.currentReplicas}/{data.desiredReplicas} réplicas ({HEALTH_LABEL[data.health]})
      </div>
      <Handle type="source" position={Position.Right} />
    </div>
  );
}
