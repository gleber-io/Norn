"""Métricas complementares da campanha (Fase 12, §3): MTTD, latência do loop decomposta em quatro
etapas, taxa de ação esperada/eficaz (ITT e por protocolo para o braço B) e taxa de fallback do LLM
decomposta por motivo. Entrada são três CSV extraídos direto do Knowledge via `psql \\copy` — não há
ferramenta .NET dedicada para isto porque são consultas de leitura pura, sem lógica de negócio nova
(§3, "análise de dado local, não código de produto" — mesmo raciocínio que já isenta `tools/analysis/`
de ser código de produto, aplicado aqui a um passo antes: extrair, não só processar).

Achado ao construir isto: `HealingPlan.DecidedBy == Fallback` não significa "o LLM falhou" — o campo é
compartilhado com o braço C (RuleEngine também cai em `Fallback` quando sua própria ação candidata é
rejeitada por pré-condição). O sinal real de falha do LLM é `LlmTrace.FailureReasons` não vazio,
independente do `DecidedBy` final (confirmado lendo `LlmPlanner.FallBackToRuleEngine`, que reusa o
`DecidedBy` do `RuleEngine.Decide` na resposta). Por isso "por protocolo" aqui filtra por
`n_failure_reasons == 0`, não por `decided_by`.
"""

from __future__ import annotations

import json
from collections import Counter

import numpy as np
import pandas as pd
from scipy import stats

REFERENCE_ACTION_BY_SCENARIO = {
    "F1": "RestartPod",
    "F2": "ScaleUp",
    "F3": "ToggleFeatureFlag",
    "F5": "NoOp",
}

ACTION_TYPE_NAMES = {0: "ScaleUp", 1: "RestartPod", 2: "ToggleFeatureFlag", 3: "NoOp"}

# Executado de verdade: excluir apenas Rejected (barreira do Executor recusou antes de tentar
# qualquer efeito colateral real) — Failed é uma tentativa real que deu errado (403 RBAC, breaker),
# e continua contando como "ação executada" para a taxa de ação eficaz (Master Plan §3).
EXECUTED_STATUSES = {"Succeeded", "PartiallyApplied", "Failed"}


def load_decisions(path: str) -> pd.DataFrame:
    df = pd.read_csv(path)
    # dtype nullable "boolean" (não "object"): fillna(False) mais abaixo não teria como decidir se
    # deve virar bool ou continuar objeto sem isto, e o pandas vem descontinuando esse downcast
    # implícito. O \copy do Postgres grava booleano como "t"/"f" (não "true"/"false"), e pandas não
    # reconhece isso como bool sozinho — mapeado explicitamente antes do cast.
    df["slo_restored"] = df["slo_restored"].map({"t": True, "f": False, True: True, False: False}).astype("boolean")
    df["action_name"] = df["action_type"].map(ACTION_TYPE_NAMES)
    df["reference_action"] = df["scenario"].map(REFERENCE_ACTION_BY_SCENARIO)
    df["matches_reference"] = df["action_name"] == df["reference_action"]
    df["failure_reasons_list"] = df["failure_reasons"].apply(
        lambda v: json.loads(v) if isinstance(v, str) and v.strip() else []
    )
    return df


def load_loop_latency(path: str) -> pd.DataFrame:
    # pd.read_csv(parse_dates=...) usa um parser rápido que exige formato uniforme na coluna — o
    # \copy do Postgres varia a precisão fracionária ("12:57:49.263+00" vs "18:18:04+00", sem
    # fração nenhuma), o que faz o parser rápido desistir e a coluna inteira virar string. to_datetime
    # com utc=True tolera a mistura.
    df = pd.read_csv(path)
    for column in ["sample_end_utc", "detected_at_utc", "context_created_at_utc", "plan_created_at_utc", "applied_at_utc"]:
        df[column] = pd.to_datetime(df[column], utc=True, format="ISO8601")
    df["deteccao_ms"] = (df["detected_at_utc"] - df["sample_end_utc"]).dt.total_seconds() * 1000
    df["correlacao_ms"] = (df["context_created_at_utc"] - df["detected_at_utc"]).dt.total_seconds() * 1000
    df["decisao_ms"] = (df["plan_created_at_utc"] - df["context_created_at_utc"]).dt.total_seconds() * 1000
    df["atuacao_ms"] = (df["applied_at_utc"] - df["plan_created_at_utc"]).dt.total_seconds() * 1000
    return df


def load_mttd(path: str) -> pd.DataFrame:
    # Mesmo motivo do load_loop_latency: to_datetime tolera precisão fracionária variável, o parser
    # rápido de parse_dates não.
    df = pd.read_csv(path)
    for column in ["onset_at_utc", "first_signal_at_utc"]:
        df[column] = pd.to_datetime(df[column], utc=True, format="ISO8601")
    df["mttd_s"] = (df["first_signal_at_utc"] - df["onset_at_utc"]).dt.total_seconds()
    return df


def median_with_ci(values: pd.Series, confidence: float = 0.95) -> tuple[float, float, float, int]:
    """Mediana + IC via bootstrap (percentile) — tempo de loop/MTTD não é normal o bastante para
    assumir simetria (§3: "mediana com IC, quando estimável", nunca média para tempo até recuperação;
    aplicamos o mesmo critério aqui por consistência, mesmo fora de H1)."""
    clean = values.dropna()
    n = len(clean)
    if n < 2:
        return (float(clean.iloc[0]) if n == 1 else float("nan"), float("nan"), float("nan"), n)
    result = stats.bootstrap(
        (clean.to_numpy(),), np.median, confidence_level=confidence, method="percentile", random_state=20260919
    )
    return (float(np.median(clean)), float(result.confidence_interval.low), float(result.confidence_interval.high), n)


def loop_latency_summary(loop_df: pd.DataFrame) -> pd.DataFrame:
    rows = []
    for stage in ["deteccao_ms", "correlacao_ms", "decisao_ms", "atuacao_ms"]:
        median, low, high, n = median_with_ci(loop_df[stage])
        rows.append({"etapa": stage, "mediana_ms": median, "ic95_low": low, "ic95_high": high, "n": n})
    return pd.DataFrame(rows)


def decision_latency_by_arm(loop_df: pd.DataFrame) -> pd.DataFrame:
    """A etapa "decisão" pooled entre os três braços esconde a pergunta que mais importa aqui: o
    braço C decide quase instantaneamente (RuleEngine puro), o braço A também (mesmo backend do
    braço C, só que em modo Observe), e só o braço B paga o custo real de uma chamada ao Ollama —
    diluir os três na mesma mediana favorece o lado com mais linhas (A+C, RuleEngine), não o
    comportamento típico de cada braço."""
    rows = []
    for arm in ["A", "B", "C"]:
        median, low, high, n = median_with_ci(loop_df[loop_df["arm"] == arm]["decisao_ms"])
        rows.append({"braco": arm, "mediana_ms": median, "ic95_low": low, "ic95_high": high, "n": n})
    return pd.DataFrame(rows)


def mttd_summary(mttd_df: pd.DataFrame) -> pd.DataFrame:
    rows = []
    for scenario in sorted(mttd_df["scenario"].unique()):
        subset = mttd_df[mttd_df["scenario"] == scenario]["mttd_s"]
        median, low, high, n = median_with_ci(subset)
        rows.append({"scenario": scenario, "mttd_mediana_s": median, "ic95_low": low, "ic95_high": high, "n": n})
    median, low, high, n = median_with_ci(mttd_df["mttd_s"])
    rows.append({"scenario": "geral", "mttd_mediana_s": median, "ic95_low": low, "ic95_high": high, "n": n})
    return pd.DataFrame(rows)


def _wilson_ci(successes: int, total: int, confidence: float = 0.95) -> tuple[float, float]:
    if total == 0:
        return (float("nan"), float("nan"))
    low, high = stats.binomtest(successes, total).proportion_ci(confidence_level=confidence, method="wilson")
    return (float(low), float(high))


def expected_action_rate(decisions_df: pd.DataFrame) -> pd.DataFrame:
    """Taxa de ação esperada por braço — ITT sempre; para B, também por protocolo (só decisões sem
    nenhum FailureReason do LLM registrado nesta decisão especifica)."""
    rows = []
    for arm in ["A", "B", "C"]:
        subset = decisions_df[decisions_df["arm"] == arm]
        matches, total = int(subset["matches_reference"].sum()), len(subset)
        low, high = _wilson_ci(matches, total)
        rows.append({"braco": arm, "protocolo": "ITT", "acertos": matches, "total": total, "taxa": matches / total if total else float("nan"), "ic95_low": low, "ic95_high": high})

        if arm == "B":
            pp = subset[subset["n_failure_reasons"] == 0]
            matches_pp, total_pp = int(pp["matches_reference"].sum()), len(pp)
            low_pp, high_pp = _wilson_ci(matches_pp, total_pp)
            rows.append({"braco": arm, "protocolo": "por_protocolo", "acertos": matches_pp, "total": total_pp, "taxa": matches_pp / total_pp if total_pp else float("nan"), "ic95_low": low_pp, "ic95_high": high_pp})
    return pd.DataFrame(rows)


def effective_action_rate(decisions_df: pd.DataFrame) -> pd.DataFrame:
    """Taxa de ação eficaz por braço — denominador é só decisões executadas de verdade
    (EXECUTED_STATUSES) que não sejam NoOp. Achado do code-reviewer: HealingActionExecutor trata
    NoOp como curto-circuito e devolve Succeeded/sloRestored=false sempre, mesmo sem tocar o
    cluster — sem excluir NoOp aqui, "ação sem efeito real" conta como "ação executada" e o
    denominador fica errado (o próprio Master Plan §5.4 já nomeia esse risco). Confirmado contra o
    dado real: a taxa do braço C ia de 12,6% para 59,7% só com esse filtro."""
    rows = []
    for arm in ["A", "B", "C"]:
        subset = decisions_df[decisions_df["arm"] == arm]
        executed = subset[subset["outcome_status"].isin(EXECUTED_STATUSES) & (subset["action_name"] != "NoOp")]
        restored, total = int(executed["slo_restored"].fillna(False).sum()), len(executed)
        low, high = _wilson_ci(restored, total)
        rows.append({"braco": arm, "protocolo": "ITT", "restauradas": restored, "executadas": total, "taxa": restored / total if total else float("nan"), "ic95_low": low, "ic95_high": high})

        if arm == "B":
            pp = executed[executed["n_failure_reasons"] == 0]
            restored_pp, total_pp = int(pp["slo_restored"].fillna(False).sum()), len(pp)
            low_pp, high_pp = _wilson_ci(restored_pp, total_pp)
            rows.append({"braco": arm, "protocolo": "por_protocolo", "restauradas": restored_pp, "executadas": total_pp, "taxa": restored_pp / total_pp if total_pp else float("nan"), "ic95_low": low_pp, "ic95_high": high_pp})
    return pd.DataFrame(rows)


def fallback_reasons_table(decisions_df: pd.DataFrame) -> pd.DataFrame:
    """Decompõe por motivo só as decisões do braço B com pelo menos um FailureReason — uma decisão
    pode ter mais de um motivo empilhado (ex.: Timeout numa tentativa, InvalidJson na retry)."""
    arm_b = decisions_df[decisions_df["arm"] == "B"]
    total_b = len(arm_b)
    counter: Counter[str] = Counter()
    for reasons in arm_b["failure_reasons_list"]:
        counter.update(reasons)

    rows = [{"motivo": reason, "ocorrencias": count, "pct_das_decisoes_b": count / total_b} for reason, count in counter.most_common()]
    return pd.DataFrame(rows)
