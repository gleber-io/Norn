"""Intervalos de confiança por bootstrap por agrupamento (execução como unidade de reamostragem).

Contextos de anomalia e ações de uma mesma execução não são independentes entre si — tratá-los como
unidades independentes subestima a variância. Aqui a reamostragem é de execuções inteiras (com
reposição), e a estatística é recalculada como razão agregada (soma dos numeradores ÷ soma dos
denominadores) sobre as execuções sorteadas. IC por percentil, semente fixa.

Escolha consciente: a reamostragem **não é estratificada por cenário**. O desenho fixa quantas
execuções há por cenário × braço, e o bootstrap deixa essa mistura variar entre réplicas — o que
alarga o intervalo (conservador). Estratificar estreitaria o IC sem mudar nenhuma conclusão.
"""

from __future__ import annotations

import numpy as np
import pandas as pd

from campaign_metrics import MASTER_SEED, executed_actions
from valid_runs import RUN_KEY

DEFAULT_RESAMPLES = 10_000


def _percentile_ci(samples: np.ndarray, confidence: float) -> tuple[float, float]:
    alpha = (1 - confidence) / 2
    return float(np.nanquantile(samples, alpha)), float(np.nanquantile(samples, 1 - alpha))


def _resampled_ratios(numerators: np.ndarray, denominators: np.ndarray, rng: np.random.Generator,
                      n_resamples: int) -> np.ndarray:
    """Razão agregada por réplica. Réplica com denominador zero (só execuções sem nenhuma ação
    executada) fica NaN e é ignorada no percentil."""
    idx = rng.integers(0, len(numerators), size=(n_resamples, len(numerators)))
    with np.errstate(invalid="ignore", divide="ignore"):
        return numerators[idx].sum(axis=1) / denominators[idx].sum(axis=1)


def paired_accuracy_difference_ci(
    paired_df: pd.DataFrame,
    n_resamples: int = DEFAULT_RESAMPLES,
    seed: int = MASTER_SEED,
    confidence: float = 0.95,
) -> dict[str, float]:
    """Diferença (acerto do LLM − acerto do RuleEngine) sobre os mesmos contextos, com IC agrupado
    por experiment_run_id."""
    per_run = paired_df.groupby("experiment_run_id").agg(
        n=("llm_matches_reference", "size"),
        llm=("llm_matches_reference", "sum"),
        rule=("rule_matches_reference", "sum"),
    )
    n, llm, rule = (per_run[c].to_numpy(dtype=float) for c in ("n", "llm", "rule"))
    point = (llm.sum() - rule.sum()) / n.sum()

    rng = np.random.default_rng(seed)
    diffs = _resampled_ratios(llm - rule, n, rng, n_resamples)
    low, high = _percentile_ci(diffs, confidence)
    return {"diferenca": float(point), "ic95_low": low, "ic95_high": high, "execucoes": len(per_run)}


def _per_run_effective(decisions_df: pd.DataFrame, arm: str) -> tuple[np.ndarray, np.ndarray]:
    """(restauradas, executadas) por execução do braço — inclui execuções sem nenhuma ação
    executada, que também são unidades do sorteio."""
    subset = decisions_df[decisions_df["arm"] == arm]
    runs = subset[RUN_KEY].drop_duplicates()
    executed = executed_actions(subset).assign(restored=lambda d: d["slo_restored"].fillna(False).astype(int))
    counts = executed.groupby(RUN_KEY).agg(n=("restored", "size"), restored=("restored", "sum")).reset_index()
    merged = runs.merge(counts, on=RUN_KEY, how="left").fillna({"n": 0, "restored": 0})
    return merged["restored"].to_numpy(dtype=float), merged["n"].to_numpy(dtype=float)


def effective_rate_difference_ci(
    decisions_df: pd.DataFrame,
    arm_minuend: str = "C",
    arm_subtrahend: str = "B",
    n_resamples: int = DEFAULT_RESAMPLES,
    seed: int = MASTER_SEED,
    confidence: float = 0.95,
) -> dict[str, float]:
    """Diferença de taxa de ação eficaz entre dois braços (denominador de
    `campaign_metrics.executed_actions`), com execuções reamostradas independentemente dentro de
    cada braço."""
    r_a, n_a = _per_run_effective(decisions_df, arm_minuend)
    r_b, n_b = _per_run_effective(decisions_df, arm_subtrahend)
    point = r_a.sum() / n_a.sum() - r_b.sum() / n_b.sum()

    rng = np.random.default_rng(seed)
    diffs = _resampled_ratios(r_a, n_a, rng, n_resamples) - _resampled_ratios(r_b, n_b, rng, n_resamples)
    low, high = _percentile_ci(diffs, confidence)
    return {"diferenca": float(point), "ic95_low": low, "ic95_high": high,
            f"execucoes_{arm_minuend}": len(n_a), f"execucoes_{arm_subtrahend}": len(n_b)}
