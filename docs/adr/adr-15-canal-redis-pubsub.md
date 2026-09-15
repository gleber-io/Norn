# ADR-15 — Transporte Worker→API por Redis pub/sub, fora do RabbitMQ

**Status:** aceito.

## Contexto
Os eventos do `NornHub` nascem na `Norn.Worker`, processo distinto da `Norn.API`, onde vive o hub SignalR e o navegador está conectado. Todos os transportes candidatos são gratuitos (MassTransit 8.x e `Microsoft.AspNetCore.SignalR.StackExchangeRedis` são MIT, RabbitMQ é MPL 2.0) — a licença não decide.

## Decisão
**Redis pub/sub**, canal `norn:events` (§5.6). `Norn.Worker` publica com `StackExchange.Redis` (já dependência de `Norn.Knowledge`/`Norn.Executor`); `Norn.API` roda um `BackgroundService` que assina e repassa ao `IHubContext<NornHub>`. **Nenhum pacote novo.**

## Alternativas descartadas
- MassTransit/RabbitMQ (já em uso) — contaminaria dado experimental: F2 degrada o RabbitMQ de propósito e `rabbitmq_queue_messages_ready` é fonte de `AnomalyContext.recentMetrics.queueDepth`. Tráfego de dashboard no mesmo broker poluiria a métrica lida como sinal.
- Backplane oficial `Microsoft.AspNetCore.SignalR.StackExchangeRedis` — resolveria o backplane multi-réplica, mas exigiria que `Norn.Worker` injetasse `IHubContext<NornHub>`, vazando apresentação para dentro do host do loop (contra a regra de dependência da §4).
- Chamada HTTP do Worker para a API — acopla os dois processos em disponibilidade.

## Consequência
Redis é plano de controle (cooldowns, flags, modo de operação, ativação do caos) e nenhum cenário de falha o degrada — o canal permanece limpo durante toda a campanha. Teste de integração da Fase 10 publica direto no Redis sem subir o Worker. Aceita-se entrega best-effort sem durabilidade, mitigada pela reidratação REST (§5.6) — a fonte de verdade é o Knowledge (ADR-06).

**Fora de escopo (produção):** se `Norn.API` escalar para mais de uma réplica, registrar o backplane oficial — decisão aqui escopada à campanha de réplica única.
