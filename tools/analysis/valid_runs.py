"""Restringe as análises complementares às execuções válidas da campanha.

O Norn.Labeler é o único lugar que decide o estado de término de cada execução (§3), e a fronteira
com o Python é o CSV que ele produz (`labeled-runs.csv`, só Recovered/CensoredAtWindowEnd). Os CSV
extraídos direto do Knowledge (`decisions.csv`, `loop-latency.csv`, `mttd.csv`) e o do
Norn.PairedAnalysis (`paired-analysis.csv`) trazem também decisões das execuções descartadas
(InvalidNoOnset). Sem este filtro, essas decisões entram nas taxas de ação esperada/eficaz, no
fallback, na latência e no recálculo pareado — ver achado metodológico 8 do results.md.
"""

from __future__ import annotations

import pandas as pd

RUN_KEY = ["run_order", "scenario", "arm"]


def _require(df: pd.DataFrame, columns: list[str], name: str) -> None:
    missing = set(columns) - set(df.columns)
    if missing:
        raise ValueError(f"Colunas ausentes em {name}: {sorted(missing)}")


def filter_to_valid_runs(df: pd.DataFrame, labeled_df: pd.DataFrame) -> pd.DataFrame:
    """Mantém só as linhas cuja execução (run_order, cenário, braço) está no CSV do Labeler."""
    _require(df, RUN_KEY, "dados a filtrar")
    _require(labeled_df, RUN_KEY, "labeled-runs")
    valid = labeled_df[RUN_KEY].drop_duplicates()
    return df.merge(valid, on=RUN_KEY, how="inner")


def filter_paired_to_valid_runs(paired_df: pd.DataFrame, labeled_df: pd.DataFrame) -> pd.DataFrame:
    """Mantém só os pares cujo experiment_run_id está no CSV do Labeler."""
    _require(paired_df, ["experiment_run_id"], "paired-analysis")
    _require(labeled_df, ["experiment_run_id"], "labeled-runs")
    valid_ids = set(labeled_df["experiment_run_id"])
    return paired_df[paired_df["experiment_run_id"].isin(valid_ids)].reset_index(drop=True)
