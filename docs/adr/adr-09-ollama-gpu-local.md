# ADR-09 — Perfil de execução local e LLM em GPU

**Status:** aceito.

## Contexto
Todo o sistema roda em um único notebook (D7): Ryzen 7 5800H, 16 GB RAM, RTX 3060 6 GB VRAM. O maior consumidor de memória seria o LLM.

## Decisão
Ollama roda **nativo no Windows com aceleração CUDA**, fora do Docker e do WSL2; o modelo reside em VRAM e não consome os 16 GB do sistema. O cluster alcança o Ollama pela rede do host. WSL2 limitado por `.wslconfig` a **8 GB e 10 processadores** — dos 16 lógicos, 6 ficam para o host (Ollama + gerador de carga). Os dois valores são congelados antes da campanha e constantes nas 60 execuções (Fase 00, tarefa 2).

## Alternativas descartadas
- Ollama em container com passagem de GPU — configuração adicional de CUDA no WSL2 sem ganho.
- Ollama em CPU — consumiria 3–5 GB da RAM e tornaria a latência do LLM o gargalo do loop.

## Consequência
Folga de VRAM (6 GB contra ~2,5 GB do modelo) permite uma segunda variante residente para análise de sensibilidade (ver ADR-12). Modelo, quantização e `num_ctx` são fixados para reprodutibilidade (§5.5).
