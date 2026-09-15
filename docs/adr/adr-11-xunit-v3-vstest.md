# ADR-11 — xUnit v3 pela ponte VSTest

**Status:** aceito.

## Contexto
`xunit.v3` está estável (4.0.1) e `xunit` v2 está em manutenção. O v3 pode executar pela ponte VSTest ou pelo Microsoft Testing Platform (MTP), no qual o projeto de teste vira executável. A escolha do executor determina a ferramenta de cobertura e o comando de CI.

## Decisão
`xunit.v3` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` + `coverlet.collector` — framework atual rodando pela ponte VSTest.

## Alternativas descartadas
- xUnit v2 — em manutenção, sem `CancellationToken` nativo nos testes.
- v3 sobre MTP — direção futura do .NET, mas trocaria `coverlet.collector` por `Microsoft.Testing.Extensions.CodeCoverage` e mudaria os comandos de CI; risco desnecessário frente ao portão de 70% de cobertura (§7.2).

## Consequência
`dotnet test` e os test explorers das IDEs continuam funcionando sem reaprendizado. Ganha-se `TestContext.Current.CancellationToken`, dando cobertura real à regra de propagação de token (§7.1) — no v2 os testes passariam `CancellationToken.None`. MTP fica como otimização futura.

**Gatilho de desistência:** se cobertura ou integração com IDE custarem mais de 2h, cair para `xunit` 2.9.3 e registrar o motivo.

**Nota (Stryker.NET):** avaliado e adiado por decisão até após a Fase 12 — exigiria o runner MTP, o que reabriria este ADR.
