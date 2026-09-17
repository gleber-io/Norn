import "@testing-library/jest-dom/vitest";
import { afterAll, afterEach, beforeAll } from "vitest";
import { server } from "./msw/server";

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

// React Flow (feature topology) mede contêineres via ResizeObserver, ausente no jsdom.
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
// biome-ignore lint/suspicious/noExplicitAny: polyfill de teste, não precisa do tipo exato do DOM lib.
(globalThis as any).ResizeObserver ??= ResizeObserverStub;
