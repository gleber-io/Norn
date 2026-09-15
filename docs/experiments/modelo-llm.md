# Modelo LLM — Norn.Planner (braço B)

## Modelo base

- Tag: `qwen3:4b-instruct-2507-q4_K_M`
- Digest (ID `ollama list`): `0edcdef34593`
- Licença: Apache 2.0
- Contexto nativo: 262.144 tokens
- Confirmado: variante *non-thinking only*, não gera blocos `<think></think>`

## Modelo derivado (o que o `Norn.Planner` consome)

- Nome: `norn-qwen:latest`
- Digest (ID `ollama list`): `fc97950356b3`
- Criado a partir de `deploy/ollama/Modelfile`, versionado no repositório
- Parâmetros fixados no Modelfile (ADR-12, §5.5):

```
FROM qwen3:4b-instruct-2507-q4_K_M
PARAMETER num_ctx 8192
PARAMETER temperature 0
PARAMETER seed 42
```

## Por que `num_ctx = 8192`

O padrão do Ollama é `OLLAMA_CONTEXT_LENGTH = 4096`. O `AnomalyContext` serializado mais o system prompt ficam na casa de 1–2 mil tokens; o orçamento de prompt do projeto é 4096 (tarefa 4 da Fase 8); 8192 deixa a outra metade para a resposta. Fixado por Modelfile e não só por chamada, porque o conector do Semantic Kernel não expõe `seed` nem `num_ctx` (§11 item 6 do plano) — uma chamada que esqueça de passar as opções ainda decide sob os parâmetros certos.

**Não subir sem motivo:** o cache KV cresce com o `num_ctx` configurado, não com o prompt usado — em 6 GB de VRAM um valor generoso empurra camadas para a CPU.

## Verificação de GPU (Fase 00 + reconfirmado na Fase 0)

```
$ ollama ps
NAME                             ID              SIZE      PROCESSOR    CONTEXT    UNTIL
qwen3:4b-instruct-2507-q4_K_M    0edcdef34593    3.2 GB    100% GPU     4096       Forever
```

100% GPU confirmado. `nvidia-smi` mostrou ~4,1 GB de VRAM em uso total na máquina (modelo + outros processos do sistema).

## Teste do derivado

`ollama run norn-qwen "responda apenas: ok"` → resposta `ok`, sem bloco de raciocínio — confirma variante Instruct, não a base híbrida.

## Variável de sensibilidade (Fase 8, tarefa 11a)

`4b-instruct-2507` × `4b-thinking-2507` — mesma família/data/tamanho/quantização, thinking mode como única variável. O pull e o `Modelfile` da variante thinking entram só na Fase 8, quando essa tarefa for executada.
