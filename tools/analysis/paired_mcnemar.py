"""H2 pareada (§3, "H2 é testada de forma pareada") — teste de McNemar sobre o CSV do
Norn.PairedAnalysis: cada linha é um contexto real do braço B, com o que o LLM decidiu de fato e o
que o RuleEngine teria decidido sobre a mesma entrada (recálculo offline, função pura)."""

from __future__ import annotations

from pathlib import Path

import pandas as pd
from statsmodels.stats.contingency_tables import mcnemar

REQUIRED_COLUMNS = {"llm_matches_reference", "rule_matches_reference"}


def load_paired(path: str | Path) -> pd.DataFrame:
    df = pd.read_csv(path)
    missing = REQUIRED_COLUMNS - set(df.columns)
    if missing:
        raise ValueError(f"Colunas ausentes em {path}: {sorted(missing)}")

    return df


def mcnemar_table(df: pd.DataFrame) -> list[list[int]]:
    """Tabela 2x2 clássica de McNemar: concordam com o gabarito (sim/sim, sim/não, não/sim, não/não)."""
    both_correct = int(((df["llm_matches_reference"] == 1) & (df["rule_matches_reference"] == 1)).sum())
    only_llm_correct = int(((df["llm_matches_reference"] == 1) & (df["rule_matches_reference"] == 0)).sum())
    only_rule_correct = int(((df["llm_matches_reference"] == 0) & (df["rule_matches_reference"] == 1)).sum())
    both_wrong = int(((df["llm_matches_reference"] == 0) & (df["rule_matches_reference"] == 0)).sum())
    return [[both_correct, only_llm_correct], [only_rule_correct, both_wrong]]


def mcnemar_result(df: pd.DataFrame) -> dict[str, float]:
    table = mcnemar_table(df)
    # exact=True é o recomendado para as contagens discordantes pequenas que uma campanha de 60
    # execuções produz — a aproximação assintótica (exact=False) não é apropriada aqui, mesmo
    # motivo que fez o §3 escolher Fisher exato em vez de qui-quadrado para n pequeno.
    result = mcnemar(table, exact=True)
    return {
        "statistic": float(result.statistic),
        "p_value": float(result.pvalue),
        "both_correct": table[0][0],
        "only_llm_correct": table[0][1],
        "only_rule_correct": table[1][0],
        "both_wrong": table[1][1],
    }


def raw_agreement_rate(df: pd.DataFrame) -> float:
    """§3: "registrar também a concordância bruta entre os dois decisores" — independente do gabarito."""
    if "llm_action" not in df.columns or "rule_action" not in df.columns:
        raise ValueError("Colunas llm_action/rule_action ausentes — não dá para calcular concordância bruta.")

    if len(df) == 0:
        return 0.0

    return float((df["llm_action"] == df["rule_action"]).mean())
