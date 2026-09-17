import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(import.meta.dirname, "./src"),
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    // fetch nativo (undici) não resolve URL relativa como o fetch de browser resolveria contra
    // document.baseURI — precisa de uma origem absoluta pro api-client montar a URL da chamada.
    // A mesma origem também precisa bater com a de `environmentOptions.jsdom.url` abaixo: é contra
    // ela que o MSW resolve os padrões relativos (`"/api/v1/topology"`) dos handlers de teste.
    env: { VITE_API_BASE_URL: "http://localhost:3000" },
    environmentOptions: {
      jsdom: { url: "http://localhost:3000/" },
    },
  },
});
