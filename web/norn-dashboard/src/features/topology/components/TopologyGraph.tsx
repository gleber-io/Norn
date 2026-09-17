import { Background, Controls, type Edge, type Node, ReactFlow } from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { useMemo } from "react";
import { QueryState } from "@/components/query-state";
import { useTopologyQuery } from "@/lib/queries";
import { useServiceHealth } from "../use-service-health";
import { ServiceNode, type ServiceNodeData } from "./ServiceNode";

const nodeTypes = { service: ServiceNode };

/** Grafo de topologia (tarefa 5 da Fase 11) — nós coloridos por saúde derivada
 * (`useServiceHealth`), navegável por teclado (`nodesFocusable`, herdado do React Flow). */
export function TopologyGraph() {
  const topologyQuery = useTopologyQuery();
  const { health } = useServiceHealth();

  const { nodes, edges } = useMemo(
    () => buildGraph(topologyQuery.data ?? [], health),
    [topologyQuery.data, health],
  );

  return (
    <QueryState
      isLoading={topologyQuery.isLoading}
      isError={topologyQuery.isError}
      error={topologyQuery.error}
      isEmpty={nodes.length === 0}
      emptyMessage="Nenhum serviço observado ainda."
    >
      <div className="h-[420px] rounded-lg border border-border">
        <ReactFlow
          nodes={nodes}
          edges={edges}
          nodeTypes={nodeTypes}
          nodesFocusable
          nodesDraggable={false}
          fitView
          proOptions={{ hideAttribution: true }}
        >
          <Background />
          <Controls showInteractive={false} />
        </ReactFlow>
      </div>
    </QueryState>
  );
}

function buildGraph(
  topology: {
    service: string;
    currentReplicas: number;
    desiredReplicas: number;
    dependsOn?: string[];
  }[],
  health: Map<string, string>,
): { nodes: Node<ServiceNodeData>[]; edges: Edge[] } {
  const depth = new Map<string, number>();
  const byService = new Map(topology.map((t) => [t.service, t]));

  const depthOf = (service: string, seen = new Set<string>()): number => {
    if (depth.has(service)) {
      return depth.get(service) as number;
    }
    if (seen.has(service)) {
      return 0;
    }
    seen.add(service);
    const deps = byService.get(service)?.dependsOn ?? [];
    const value = deps.length === 0 ? 0 : 1 + Math.max(...deps.map((d) => depthOf(d, seen)));
    depth.set(service, value);
    return value;
  };

  const columns = new Map<number, number>();
  const nodes: Node<ServiceNodeData>[] = topology.map((t) => {
    const d = depthOf(t.service);
    const row = columns.get(d) ?? 0;
    columns.set(d, row + 1);
    const serviceHealth = (health.get(t.service) ?? "healthy") as ServiceNodeData["health"];
    return {
      id: t.service,
      type: "service",
      position: { x: d * 240, y: row * 120 },
      ariaLabel: `${t.service}, estado ${serviceHealth}, ${t.currentReplicas} de ${t.desiredReplicas} réplicas`,
      data: {
        label: t.service,
        currentReplicas: t.currentReplicas,
        desiredReplicas: t.desiredReplicas,
        health: serviceHealth,
      },
    };
  });

  const edges: Edge[] = topology.flatMap((t) =>
    (t.dependsOn ?? [])
      .filter((dep) => byService.has(dep))
      .map((dep) => ({ id: `${dep}->${t.service}`, source: dep, target: t.service })),
  );

  return { nodes, edges };
}
