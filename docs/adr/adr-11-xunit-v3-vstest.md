# ADR-11 — xUnit v3 sobre Microsoft Testing Platform (MTP)

**Status:** aceito (revisado 14/09/2026 — decisão original invalidada pelo SDK, não por escolha).

## Contexto
`xunit.v3` está estável (4.0.1) e `xunit` v2 está em manutenção. O v3 pode executar pela ponte VSTest ou pelo Microsoft Testing Platform (MTP), no qual o projeto de teste vira executável. A escolha do executor determina a ferramenta de cobertura e o comando de CI.

## Decisão original (14/09/2026, invalidada no mesmo dia)
`xunit.v3` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` + `coverlet.collector` — framework atual rodando pela ponte VSTest.

**Por que caiu, na prática e não por preferência:** ao rodar `dotnet test` na Fase 1 (primeiro projeto de teste do repositório), o SDK 10.0.401 recusou a execução: *"Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later."* O Microsoft.Testing.Platform 2.x embutido no xunit.v3 4.0.x **removeu o suporte a VSTest para projetos `net10.0`** — não é uma opção de configuração revertida por flag; é o par SDK 10 + xunit.v3 4.0.x que não oferece esse caminho. Confirmado contra [dotnet/sdk#51283](https://github.com/dotnet/sdk/issues/51283) e reports equivalentes em outros projetos que fizeram o mesmo upgrade.

## Decisão revisada
`xunit.v3` (4.0.1) sobre **Microsoft.Testing.Platform**, framework atual. Mudanças de configuração:
- `global.json` ganha `"test": { "runner": "Microsoft.Testing.Platform" }` — opta o `dotnet test` para o novo executor.
- Projeto de teste ganha `<OutputType>Exe</OutputType>` — sob MTP, o projeto de teste **é** o executável.
- Saem `xunit.runner.visualstudio` (adaptador específico de VSTest) e `Microsoft.NET.Test.Sdk` (bootstrap de VSTest) — nenhum dos dois tem papel sob MTP.
- Cobertura troca `coverlet.collector` por **`Microsoft.Testing.Extensions.CodeCoverage`** (18.11.2) — `--collect:"XPlat Code Coverage"` vira a flag `--coverage`.

## Alternativas descartadas
- xUnit v2 — em manutenção, sem `CancellationToken` nativo nos testes; era o gatilho de desistência original, mas não resolveria nada aqui, já que o problema é do **SDK 10 + xunit.v3**, não do framework de teste em si — v2 sequer estava em jogo.
- Manter VSTest — **não é mais uma opção** para `net10.0` com este SDK, ver acima.

## Consequência
O comando de CI muda (`dotnet test --coverage` no lugar de `--collect:"XPlat Code Coverage"`), e a integração com o test explorer da IDE passa a depender do suporte a MTP da própria IDE (mais recente em Visual Studio/Rider/VS Code atualizados) em vez do protocolo VSTest. Mantém-se `TestContext.Current.CancellationToken`, que já era do xunit.v3 e não depende do executor.

**Nota (Stryker.NET):** o runner MTP, que a §7.2 listava como pré-requisito para adotar Stryker.NET 5.0.0, **já está em uso desde a Fase 1** — não por escolha, mas por obrigação do SDK. A condição 1 da §7.2 (item de "três condições para tentar") está satisfeita antes do previsto; as condições 2 e 3 (validar contra bug conhecido, orçamento de tempo) continuam de pé, e a avaliação continua adiada para depois da Fase 12.
