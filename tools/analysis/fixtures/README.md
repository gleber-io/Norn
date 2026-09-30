# Fixtures sintéticas

Os CSV neste diretório são dados **inventados**, só para exercitar `survival_analysis.py`,
`paired_mcnemar.py` e `analyze.py` sem depender do Postgres/Prometheus reais. Não são resultado da
campanha (Fase 12) — números da campanha real vão para `tools/analysis/data/`, nunca aqui.

As chaves de execução (`run_order`, `scenario`, `arm` e `experiment_run_id`) de todas as fixtures
existem em `labeled-runs.sample.csv`: o `analyze.py` restringe as entradas complementares às
execuções desse arquivo e falha se o filtro esvaziar uma entrada (`tests/test_analyze.py`).
