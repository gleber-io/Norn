"""Entrada única da análise da campanha (Fase 12, tarefas 5-6). Lê os dois CSV que .NET produz
(Norn.Labeler e Norn.PairedAnalysis) e escreve um resumo em Markdown + os gráficos de Kaplan-Meier
em `--out-dir`. Fora do CI (§3, "é análise de dado local, não código de produto")."""

from __future__ import annotations

import argparse
from pathlib import Path

from campaign_metrics import (
    decision_latency_by_arm,
    effective_action_rate,
    expected_action_rate,
    fallback_reasons_table,
    load_decisions,
    load_loop_latency,
    load_mttd,
    loop_latency_summary,
    mttd_summary,
)
from paired_mcnemar import load_paired, mcnemar_result, raw_agreement_rate
from survival_analysis import (
    fisher_recovery_rate,
    kaplan_meier_curves,
    load_labeled_runs,
    logrank_by_scenario,
    recovery_rate_table,
)


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
        paired_df = load_paired(args.paired)
        result = mcnemar_result(paired_df)
        agreement = raw_agreement_rate(paired_df)

        lines.append("## H2 — recálculo pareado (LLM x RuleEngine, braço B)")
        lines.append("")
        lines.append(f"- Pares: {len(paired_df)}")
        lines.append(f"- Concordância bruta LLM x RuleEngine: {agreement:.4f}")
        lines.append(f"- McNemar: estatística={result['statistic']:.4f}, p={result['p_value']:.4f}")
        lines.append(
            f"- Tabela: ambos certos={result['both_correct']}, só LLM certo={result['only_llm_correct']}, "
            f"só regra certa={result['only_rule_correct']}, ambos errados={result['both_wrong']}"
        )
        lines.append("")

    if args.decisions:
        decisions_df = load_decisions(args.decisions)

        lines.append("## Taxa de ação esperada e taxa de ação eficaz (ITT x por protocolo)")
        lines.append("")
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

    if args.loop_latency:
        loop_df = load_loop_latency(args.loop_latency)

        lines.append("## Latência do loop, decomposta em quatro etapas (mediana + IC 95%, ms)")
        lines.append("")
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
        mttd_df = load_mttd(args.mttd)

        lines.append("## MTTD — tempo até a detecção (mediana + IC 95%, segundos)")
        lines.append("")
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
