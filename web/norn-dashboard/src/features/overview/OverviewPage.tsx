import { Timeline } from "@/features/mapek/components/Timeline";
import { useSeedTimelineFromRest } from "@/features/mapek/use-seed-timeline";
import { TopologyGraph } from "@/features/topology/components/TopologyGraph";

/** Página padrão ("/") — grafo de topologia + timeline MAPE-K na mesma tela, sem navegação, porque
 * o roteiro do DoD da Fase 11 (injetar F1 e ver o ciclo completo) precisa acontecer numa página só. */
export function OverviewPage() {
  useSeedTimelineFromRest();

  return (
    <div className="flex flex-col gap-6">
      <section aria-label="Topologia do Shop">
        <h2 className="mb-3 text-sm font-semibold text-muted-foreground">Topologia</h2>
        <TopologyGraph />
      </section>
      <section aria-label="Linha do tempo MAPE-K">
        <h2 className="mb-3 text-sm font-semibold text-muted-foreground">Linha do tempo</h2>
        <Timeline />
      </section>
    </div>
  );
}
