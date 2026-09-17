import { useQueryClient } from "@tanstack/react-query";
import { AppRoutes } from "@/app/router";
import { Layout } from "@/components/Layout";
import { useNornHub } from "@/lib/signalr";

export function App() {
  const queryClient = useQueryClient();
  useNornHub(queryClient);

  return (
    <Layout>
      <AppRoutes />
    </Layout>
  );
}
