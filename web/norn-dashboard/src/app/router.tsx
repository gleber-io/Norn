import { Route, Routes } from "react-router";
import { MetricsPage } from "@/features/metrics/MetricsPage";
import { OverviewPage } from "@/features/overview/OverviewPage";
import { PlanDetailPage } from "@/features/plans/PlanDetailPage";
import { PlansListPage } from "@/features/plans/PlansListPage";

/** React Router v8, modo declarative (§7.3 — nunca createBrowserRouter/RouterProvider, que é
 * modo data). Poucas rotas; estado de servidor é do TanStack Query, não do router. */
export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<OverviewPage />} />
      <Route path="/plans" element={<PlansListPage />} />
      <Route path="/plans/:planId" element={<PlanDetailPage />} />
      <Route path="/metrics" element={<MetricsPage />} />
    </Routes>
  );
}
