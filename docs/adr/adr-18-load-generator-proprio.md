# ADR-18 — Gerador de carga próprio, sem NBomber

**Status:** aceito.

## Contexto
A Fase 5 (tarefa 4) previa `Norn.LoadGenerator` com NBomber para o perfil senoidal de carga ("dia" = 15 min, amostragem 5 s, seed fixo). Verificação em 15/09/2026: a partir da **v5**, o pacote NuGet do NBomber passou a ser licenciado sob a **NBomber Business License 1.1** — gratuito só para uso pessoal; uso organizacional (empresa, time de pesquisa, instituição) exige licença Business ou Enterprise paga. O código no GitHub é Apache-2.0, mas o **pacote publicado no NuGet** — o que o `PackageReference` de fato consome — é que carrega a licença restritiva. A última major sob MIT foi a v4.x, descontinuada desde a mudança de licenciamento.

## Decisão
**Console próprio em `Norn.LoadGenerator`**, sem NBomber nem qualquer outro framework de carga: `HttpClient` + `PeriodicTimer` implementando o perfil senoidal diretamente — função pura de taxa em função do tempo decorrido, seed fixo, sem I/O dentro do cálculo da taxa. Mesmo padrão do ADR-13 (caos sem pacote de terceiros).

## Alternativas descartadas
- Fixar NBomber 4.x (última MIT) — evita a licença, mas é uma major descontinuada, sem garantia de compatibilidade nativa com .NET 10 e sem correções desde a mudança de licenciamento.
- Licença Business/Enterprise do NBomber — decisão financeira fora do escopo de um TCC, e desnecessária: o perfil exigido (senoidal, amostragem de 5 s, HTTP simples) não usa nenhum recurso diferenciado do NBomber (distribuição, cluster, protocolos não-HTTP).

## Consequência
`Norn.LoadGenerator` continua sem nenhuma referência de projeto (fala HTTP com o Shop, §4), e também sem nenhum `PackageReference` de terceiros. Em troca, a orquestração de taxa/concorrência que o NBomber daria pronta — inclusive o relatório de taxa alcançada vs. pretendida, exigido pela §3 para marcar `InvalidInstrumentation` — é código próprio a manter.
