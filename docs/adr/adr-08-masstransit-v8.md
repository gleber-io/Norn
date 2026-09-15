# ADR-08 — Biblioteca de mensageria: MassTransit v8 fixado

**Status:** aceito.

## Contexto
A partir da v9, MassTransit adotou modelo comercial, incompatível com um trabalho acadêmico publicado que deve permanecer livremente reprodutível. Verificado em 14/09/2026: a v9.2.1 declara no próprio pacote *"MassTransit is a commercial product that must be licensed"*; a série 8 é **Apache-2.0** (não MIT, como se supunha antes da verificação), com a linha encerrada. Reverificado em 14/09/2026 (Fase 0): última 8.x estável no NuGet é **8.5.10**.

## Decisão
Fixar `MassTransit` numa **versão 8.x exata** (`8.5.10`) em `Directory.Packages.props`, com trava explícita contra atualização para v9+.

## Alternativas descartadas
- `RabbitMQ.Client` puro — reimplementar outbox, retry, DLQ e idempotência à mão, custo alto para o prazo do TCC.
- `Rebus` / `CAP` (MIT) — viáveis, mas exigem integração manual de outbox com EF Core/Postgres e têm menor familiaridade na comunidade .NET.

## Consequência
`Norn.BuildingBlocks.Messaging` encapsula MassTransit v8; qualquer bump para v9+ exige revisar este ADR. Outbox/inbox são **configuração** nativa da v8: `AddEntityFrameworkOutbox<TDbContext>(o => { o.UsePostgres(); o.UseBusOutbox(); })`, mais `AddInboxStateEntity()`, `AddOutboxMessageEntity()`, `AddOutboxStateEntity()`. A deduplicação do inbox é por **`MessageId` por endpoint** — o `eventId` do envelope (§5.1) **deve ser** o `MessageId` do MassTransit.
