"""Entrada única da análise da campanha (Fase 12, tarefas 5-6). Lê os dois CSV que .NET produz
(Norn.Labeler e Norn.PairedAnalysis) e escreve um resumo em Markdown + os gráficos de Kaplan-Meier
em `--out-dir`. Fora do CI (§3, "é análise de dado local, não código de produto")."""

from __future__ import annotations

import argparse
from pathlib import Path

from campaign_metrics import (
    decision_latency_by_arm,
    effective_action_rate,
    effective_action_rate_by_type,
    expected_action_rate,
    fallback_by_scenario,
    fallback_reasons_table,
    load_decisions,
    load_loop_latency,
    load_mttd,
    loop_latency_summary,
    mttd_summary,
    restoration_per_decision,
)
from cluster_bootstrap import effective_rate_difference_ci, paired_accuracy_difference_ci
from paired_mcnemar import load_paired, mcnemar_result, raw_agreement_rate
from survival_analysis import (
    fisher_recovery_rate,
    kaplan_meier_curves,
    load_labeled_runs,
    logrank_by_scenario,
    recovery_rate_table,
)
from valid_runs import RUN_KEY, filter_paired_to_valid_runs, filter_to_valid_runs

PAIRED_RUN_KEY = ["experiment_run_id"]


def _restrict(df, labeled_df, filter_fn, run_key: list[str], name: str):
    """Aplica o filtro de execuções válidas; falha se ele esvaziar uma entrada não vazia (chaves que
    não batem com o labeled-runs.csv) e devolve a linha de relatório com o que saiu."""
    filtered = filter_fn(df, labeled_df)
    if len(df) and filtered.empty:
        raise ValueError(f"{name}: nenhuma linha bate com as execuções do labeled-runs.csv")
    runs_removed = len(df[run_key].drop_duplicates()) - len(filtered[run_key].drop_duplicates())
    note = (f"_{name}: {len(filtered)} linhas de execuções válidas; {len(df) - len(filtered)} linhas de "
            f"{runs_removed} execuções descartadas removidas._")
    return filtered, note


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--labeled", required=True, help="CSV do Norn.Labeler (labeled-runs.csv)")
    parser.add_argument("--paired", help="CSV do Norn.PairedAnalysis (paired-analysis.csv) — opcional, só H2")
    parser.add_argument("--decisions", help="CSV de decisões extraído do Knowledge (ação esperada/eficaz, ITT x por protocolo, fallback por motivo) — opcional")
    parser.add_argument("--loop-latency", help="CSV de latência do loop extraído do Knowledge (quatro etapas) — opcional")
    parser.add_argument("--mttd", help="CSV de MTTD extraído do Knowledge — opcional")
    parser.add_argument("--out-dir", required=True, help="Diretório de saída para gráficos e resumo.md")
    args = parser.parse_args()

    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    lines: list[str] = ["# Resumo da análise da campanha", ""]

    labeled_df = load_labeled_runs(args.labeled)
    lines.append("## H1 — taxa de recuperação e tempo até a recuperação")
    lines.append("")
    lines.append("```")
    lines.append(recovery_rate_table(labeled_df).to_string(index=False))
    lines.append("```")
    lines.append("")

    for scenario in sorted(labeled_df["scenario"].unique()):
        png_path = kaplan_meier_curves(labeled_df, scenario, out_dir)
        logrank = logrank_by_scenario(labeled_df, scenario)
        fisher = fisher_recovery_rate(labeled_df, scenario)

        lines.append(f"### {scenario}")
        lines.append(f"![Kaplan-Meier {scenario}]({png_path.name})")
        lines.append("")
        lines.append("Log-rank (tempo até a recuperação, primário):")
        for key, value in logrank.items():
            lines.append(f"- {key}: p={value:.4f}")
        lines.append("")
        lines.append("Fisher exato (taxa de recuperação na janela, secundário):")
        for key, value in fisher.items():
            lines.append(f"- {key}: p={value:.4f}")
        lines.append("")

    if args.paired:
        lines.append("## H2 — recálculo pareado (LLM x RuleEngine, braço B)")
        lines.append("")
        paired_df, note = _restrict(load_paired(args.paired), labeled_df, filter_paired_to_valid_runs,
                                    PAIRED_RUN_KEY, "paired-analysis")
        lines.extend([note, ""])
        result = mcnemar_result(paired_df)
        agreement = raw_agreement_rate(paired_df)
        cluster = paired_accuracy_difference_ci(paired_df)

        lines.append(f"- Pares: {len(paired_df)} (de {paired_df['experiment_run_id'].nunique()} execuções)")
        lines.append(
            f"- Acerto da ação de referência: LLM {int(paired_df['llm_matches_reference'].sum())}/{len(paired_df)}, "
            f"RuleEngine {int(paired_df['rule_matches_reference'].sum())}/{len(paired_df)}"
        )
        lines.append(f"- Concordância bruta LLM x RuleEngine: {agreement:.4f}")
        lines.append(f"- McNemar: estatística={result['statistic']:.4f}, p={result['p_value']:.4f}")
        lines.append(
            f"- Tabela: ambos certos={result['both_correct']}, só LLM certo={result['only_llm_correct']}, "
            f"só regra certa={result['only_rule_correct']}, ambos errados={result['both_wrong']}"
        )
        lines.append(
            f"- Diferença de acerto (LLM − RuleEngine) com IC 95% por bootstrap agrupado por execução: "
            f"{cluster['diferenca']:.4f} [{cluster['ic95_low']:.4f}; {cluster['ic95_high']:.4f}] "
            f"({cluster['execucoes']} execuções reamostradas)"
        )
        lines.append("")

    if args.decisions:
        lines.append("## Taxa de ação esperada e taxa de ação eficaz (ITT x por protocolo)")
        lines.append("")
        decisions_df, note = _restrict(load_decisions(args.decisions), labeled_df, filter_to_valid_runs,
                                       RUN_KEY, "decisions")
        lines.extend([note, ""])
        lines.append(
            "\"Por protocolo\" no braço B restringe às decisões sem nenhum FailureReason do LLM "
            "registrado (LlmTrace.FailureReasons vazio) — DecidedBy sozinho não serve de filtro "
            "porque também vale Fallback quando o RuleEngine do braço C rejeita a própria ação "
            "candidata, sem nenhuma falha de LLM envolvida."
        )
        lines.append("")
        lines.append("Ação esperada (decisão bate com a ação de referência do cenário):")
        lines.append("```")
        lines.append(expected_action_rate(decisions_df).to_string(index=False))
        lines.append("```")
        lines.append("")
        lines.append("Ação eficaz (SLO restaurado ÷ ações executadas — Succeeded/PartiallyApplied/Failed, exclui Rejected):")
        lines.append("```")
        lines.append(effective_action_rate(decisions_df).to_string(index=False))
        lines.append("```")
        lines.append("")
        eff_ci = effective_rate_difference_ci(decisions_df)
        lines.append(
            f"Diferença de ação eficaz C − B com IC 95% por bootstrap agrupado por execução: "
            f"{eff_ci['diferenca']:.4f} [{eff_ci['ic95_low']:.4f}; {eff_ci['ic95_high']:.4f}]. "
            "Medida descritiva, condicionada às ações que cada braço escolheu executar:"
        )
        lines.append("```")
        lines.append(effective_action_rate_by_type(decisions_df).to_string(index=False))
        lines.append("```")
        lines.append("")
        lines.append(
            "Restauração verificada ÷ total de decisões do braço — denominador independente da composição "
            "das ações, mas não do número de decisões por execução de cada braço; não é inversão da taxa acima:"
        )
        lines.append("```")
        lines.append(restoration_per_decision(decisions_df).to_string(index=False))
        lines.append("```")
        lines.append("")

        lines.append("## Taxa de fallback do LLM decomposta por motivo (braço B)")
        lines.append("")
        fallback_table = fallback_reasons_table(decisions_df)
        if fallback_table.empty:
            lines.append("Nenhuma decisão do braço B registrou FailureReason.")
        else:
            lines.append("```")
            lines.append(fallback_table.to_string(index=False))
            lines.append("```")
            lines.append("")
            lines.append("Decisões com contingência (ao menos um motivo), por cenário:")
            lines.append("```")
            lines.append(fallback_by_scenario(decisions_df).to_string(index=False))
            lines.append("```")
        lines.append("")

    if args.loop_latency:
        lines.append("## Latência do loop, decomposta em quatro etapas (mediana + IC 95%, ms)")
        lines.append("")
        loop_df, note = _restrict(load_loop_latency(args.loop_latency), labeled_df, filter_to_valid_runs,
                                  RUN_KEY, "loop-latency")
        lines.extend([note, ""])
        lines.append(
            "A etapa de correlação inclui a janela de 60s da Fase 7 por desenho — é latência de "
            "projeto, não ineficiência do laço (Master Plan §3)."
        )
        lines.append("")
        lines.append("```")
        lines.append(loop_latency_summary(loop_df).to_string(index=False))
        lines.append("```")
        lines.append("")
        lines.append(
            "Etapa de decisão por braço — a mediana acima é pooled entre A/B/C, e A+C (RuleEngine "
            "quase instantâneo) são a maioria das linhas; a diferença real só aparece separando por "
            "braço:"
        )
        lines.append("```")
        lines.append(decision_latency_by_arm(loop_df).to_string(index=False))
        lines.append("```")
        lines.append("")

    if args.mttd:
        lines.append("## MTTD — tempo até a detecção (mediana + IC 95%, segundos)")
        lines.append("")
        mttd_df, note = _restrict(load_mttd(args.mttd), labeled_df, filter_to_valid_runs, RUN_KEY, "mttd")
        lines.extend([note, ""])
        lines.append("```")
        lines.append(mttd_summary(mttd_df).to_string(index=False))
        lines.append("```")
        lines.append("")

    lines.append("## Overhead do Norn")
    lines.append("")
    lines.append(
        "Não mensurável nesta campanha: a métrica exige CPU/memória dos Pods do Norn via cAdvisor "
        "do kubelet (Master Plan §3), mas Norn.Worker/Norn.API rodaram como processos no host "
        "(`dotnet run`) durante toda a campanha real, não como Pods no cluster — decisão operacional "
        "registrada desde a Fase 9/10/11, nunca revertida. Não há série de cAdvisor para o Norn "
        "neste dataset. Medir isso exigiria rodar a campanha (ou parte dela) com os manifestos "
        "`norn-platform.yaml`/`norn-api.yaml` já empacotados, mas nunca ligados ao `bootstrap.ps1`."
    )
    lines.append("")

    summary_path = out_dir / "resumo.md"
    summary_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"Resumo escrito em {summary_path}")


if __name__ == "__main__":
    main()
