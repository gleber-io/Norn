# ADR-17 — Clean Architecture com vertical slice na borda

**Status:** aceito.

## Contexto
O plano prescrevia Minimal API em vertical slice (§7.1) e uma regra de dependência **entre serviços** (§4), mas nenhuma regra **entre camadas**. Consequência: `Norn.Analyzer`, `Norn.Planner` e `Norn.Executor` referenciavam `Norn.Knowledge`, que carrega `Npgsql.EntityFrameworkCore.PostgreSQL` e `StackExchange.Redis`. Os três projetos que **são** a contribuição do trabalho — sob o portão de 70% de cobertura (§7.2) — dependiam do detalhe de persistência.

## Decisão
Inverter a dependência em `Platform/` por projeto, e no `Shop/` por pasta. Portas (`IKnowledgeStore`, `IPlatformConfig`, `ICooldownStore`, `ITopologyReader`, `IMetricSource`) declaradas em `Norn.Contracts`, que vira o núcleo com **zero pacotes NuGet**; `Norn.Knowledge` vira o adaptador; `Norn.Worker` e `Norn.API` são os únicos a referenciá-lo, como composition roots. No Shop, cada API mantém um projeto com quatro pastas — `Domain`, `Application`, `Infrastructure`, `Features` — validadas por namespace.

**Vertical slice e Clean Architecture não competem:** a primeira diz *como agrupar* (por feature); a segunda diz *para onde as setas apontam* (para dentro, sempre). `Features/ReserveStock/Handler.cs` dependendo de `IStockRepository` declarada em `Application/` satisfaz as duas. Pastas `Controllers/`, `Services/`, `Repositories/` genéricas continuam proibidas.

## Alternativas descartadas
- Projeto por camada no Shop (3×4=12 projetos) — mais canônico, mas multiplica build/CI e reescreveria as Fases 2 e 3 já planejadas, sem mudar nenhuma hipótese.
- Manter só vertical slice, sem regra de camada — estado anterior, com a inversão quebrada.
- Inverter só na plataforma, deixando o Shop intacto — deixaria duas filosofias sem critério declarado.

## Consequência
Testar `RuleEngine`, os detectores do Analyzer e as barreiras do ADR-04 deixa de arrastar o grafo do EF Core — dublê de porta em vez de Testcontainers em teste unitário. `Norn.ArchitectureTests` ganha duas famílias de regra além das três de projeto.

**Custo declarado:** uma indireção a mais entre caso de uso e banco; disciplina de nunca declarar porta com um único uso trivial.

**Limitação declarada:** `Norn.Monitor` e `Norn.Executor` permanecem adaptadores com `KubernetesClient` próprio — falar com o cluster **é** a responsabilidade deles; inverter ali seria cerimônia sem ganho.
