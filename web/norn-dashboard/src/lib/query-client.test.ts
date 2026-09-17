import { describe, expect, it } from "vitest";
import { queryKeys } from "./query-client";

/**
 * Regressão: `signals`/`plans`/`outcomes` precisam levar `limit` na queryKey — sem isso, duas
 * telas com limites diferentes (ex. lista com 100, detalhe com 200) disputam a mesma entrada de
 * cache do TanStack Query, e qual limite "vence" depende da ordem de montagem dos componentes,
 * não de quem está lendo (achado do code-reviewer antes do commit deste bloco da Fase 11).
 */
describe("queryKeys", () => {
  it("plans com limites diferentes gera chaves diferentes", () => {
    expect(queryKeys.plans(100)).not.toEqual(queryKeys.plans(200));
  });

  it("outcomes com limites diferentes gera chaves diferentes", () => {
    expect(queryKeys.outcomes(50)).not.toEqual(queryKeys.outcomes(200));
  });

  it("signals com limites diferentes gera chaves diferentes", () => {
    expect(queryKeys.signals(50)).not.toEqual(queryKeys.signals(200));
  });

  it("signals com o mesmo limite e experimentRunId gera a mesma chave", () => {
    expect(queryKeys.signals(50, "run-1")).toEqual(queryKeys.signals(50, "run-1"));
  });
});
