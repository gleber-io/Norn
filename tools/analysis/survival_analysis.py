"""Kaplan-Meier + log-rank (H1, primário) e Fisher exato (H1, secundário) — Master Plan §3.

Lê o CSV do Norn.Labeler (a fronteira estreita entre .NET e Python, §3: "nada em Python lê o
PostgreSQL ou o Prometheus diretamente"). Cada linha é uma execução válida (Recovered ou
CensoredAtWindowEnd — Invalid* nunca chegam a este arquivo, ver Norn.Labeler/Csv/CampaignCsv.cs).
"""

from __future__ import annotations

import itertools
from pathlib import Path

import pandas as pd
from lifelines import KaplanMeierFitter
from lifelines.statistics import multivariate_logrank_test, pairwise_logrank_test
from scipy.stats import fisher_exact

REQUIRED_COLUMNS = {
    "scenario",
    "arm",
    "termination_state",
    "tempo_ate_recuperacao_segundos",
    "evento_observado",
}


def load_labeled_runs(path: str | Path) -> pd.DataFrame:
    df = pd.read_csv(path)
    missing = REQUIRED_COLUMNS - set(df.columns)
    if missing:
        raise ValueError(f"Colunas ausentes em {path}: {sorted(missing)}")

    return df


def kaplan_meier_curves(df: pd.DataFrame, scenario: str, out_dir: Path) -> Path:
    """Curvas de KM dos três braços sobrepostas, com marcas de censura — um PNG por cenário."""
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    subset = df[df["scenario"] == scenario]
    fig, ax = plt.subplots(figsize=(8, 5))
    for arm in sorted(subset["arm"].unique()):
        arm_data = subset[subset["arm"] == arm]
        fitter = KaplanMeierFitter(label=f"Braço {arm}")
        fitter.fit(
            arm_data["tempo_ate_recuperacao_segundos"],
            event_observed=arm_data["evento_observado"],
        )
        fitter.plot_survival_function(ax=ax, show_censors=True)

    ax.set_title(f"Kaplan-Meier — {scenario}")
    ax.set_xlabel("Tempo até a recuperação (s)")
    ax.set_ylabel("Proporção não recuperada")

    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / f"kaplan-meier-{scenario}.png"
    fig.savefig(out_path, dpi=150, bbox_inches="tight")
    plt.close(fig)
    return out_path


def logrank_by_scenario(df: pd.DataFrame, scenario: str) -> dict[str, float]:
    """Log-rank multivariado (3 braços) e pareado (cada par) — §3: "comparadas entre braços por teste log-rank"."""
    subset = df[df["scenario"] == scenario]
    arms = sorted(subset["arm"].unique())
    results: dict[str, float] = {}

    if len(arms) >= 2:
        overall = multivariate_logrank_test(
            subset["tempo_ate_recuperacao_segundos"],
            subset["arm"],
            subset["evento_observado"],
        )
        results["overall_p_value"] = overall.p_value

    if len(arms) >= 2:
        pairwise = pairwise_logrank_test(
            subset["tempo_ate_recuperacao_segundos"],
            subset["arm"],
            subset["evento_observado"],
        )
        for arm_a, arm_b in itertools.combinations(arms, 2):
            # A ordem do par no índice segue a ordem de aparição em `groups`, não `sorted(arms)` —
            # tenta as duas orientações em vez de presumir qual delas o lifelines produziu.
            if (arm_a, arm_b) in pairwise.summary.index:
                results[f"{arm_a}_vs_{arm_b}_p_value"] = pairwise.summary.loc[(arm_a, arm_b), "p"]
            else:
                results[f"{arm_a}_vs_{arm_b}_p_value"] = pairwise.summary.loc[(arm_b, arm_a), "p"]

    return results


def fisher_recovery_rate(df: pd.DataFrame, scenario: str) -> dict[str, float]:
    """§3, secundário: taxa de recuperação na janela, teste exato de Fisher, apropriado a n=5."""
    subset = df[df["scenario"] == scenario]
    arms = sorted(subset["arm"].unique())
    results: dict[str, float] = {}

    for arm_a, arm_b in itertools.combinations(arms, 2):
        table = _recovery_contingency_table(subset, arm_a, arm_b)
        _, p_value = fisher_exact(table)
        results[f"{arm_a}_vs_{arm_b}_p_value"] = p_value

    return results


def _recovery_contingency_table(df: pd.DataFrame, arm_a: str, arm_b: str) -> list[list[int]]:
    def counts(arm: str) -> tuple[int, int]:
        arm_rows = df[df["arm"] == arm]
        recovered = int((arm_rows["termination_state"] == "Recovered").sum())
        not_recovered = int((arm_rows["termination_state"] != "Recovered").sum())
        return recovered, not_recovered

    recovered_a, not_recovered_a = counts(arm_a)
    recovered_b, not_recovered_b = counts(arm_b)
    return [[recovered_a, not_recovered_a], [recovered_b, not_recovered_b]]


def recovery_rate_table(df: pd.DataFrame) -> pd.DataFrame:
    """Taxa de recuperação por (cenário × braço) — §3, "execuções com Recovered ÷ execuções válidas"."""
    grouped = df.groupby(["scenario", "arm"])
    return grouped["evento_observado"].agg(["sum", "count"]).rename(
        columns={"sum": "recovered", "count": "total"}
    ).assign(recovery_rate=lambda t: t["recovered"] / t["total"]).reset_index()
