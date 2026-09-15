# ADR-12 — Modelo do braço B: Qwen3 4B Instruct-2507

**Status:** aceito.

## Contexto
O braço B (§3) precisa de um LLM local que produza JSON válido contra o catálogo fechado de ações, caiba nos 6 GB de VRAM da RTX 3060 (ADR-09) e permita reprodutibilidade por digest fixo.

## Decisão
**Qwen3 4B Instruct-2507 quantizado em Q4** (`qwen3:4b-instruct-2507-q4_K_M`, 2,5 GB), sob **Apache 2.0**. Confirmado: a variante suporta apenas modo não-thinking e não gera blocos `<think></think>`. Contexto nativo de 262.144 tokens — por isso `num_ctx` precisa ser fixado (§5.5), não herdado do padrão do Ollama.

## Alternativas descartadas
- `llama3.2:3b-instruct` — licença Llama Community, não aprovada pela OSI; geração anterior, mais fraco em saída estruturada.
- Qwen2.5 3B — sob Qwen Research License (não-comercial).
- Phi-3.5-mini 3.8B — MIT, mas tende a emitir preâmbulo antes do JSON.

## O que caiu: análise de sensibilidade 4B×8B
Não existe `qwen3:8b-instruct-2507` — os únicos 8B da biblioteca são o híbrido `qwen3:8b` original. Comparar mudaria duas variáveis (tamanho e geração/modo) ao mesmo tempo.

## O que entra no lugar
**`4b-instruct-2507` × `4b-thinking-2507`** (tarefa 11a da Fase 8) — mesma família, data, tamanho e quantização; a única variável é o *thinking mode*. Mede a tese central deste ADR: se raciocínio explícito degrada aderência a saída estruturada sob contrato fechado.

## Consequência
Usar obrigatoriamente a variante **Instruct**, nunca a base (híbrida, thinking ativo por padrão — inflaria latência p99 e poluiria o parsing).

**Gatilho de desistência:** se o modelo não fechar o DoD da Fase 0 (residente em VRAM, JSON válido contra o catálogo), cair para `llama3.2:3b-instruct-q4_K_M` e registrar o motivo.

**Nota para a defesa:** o modelo é variável controlada, idêntica nos três braços; a contribuição do trabalho é a arquitetura MAPE-K e a comparação A/B/C, não a escolha do LLM.
