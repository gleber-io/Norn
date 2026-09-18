"""Entrada única da análise da campanha (Fase 12, tarefas 5-6). Lê os dois CSV que .NET produz
(Norn.Labeler e Norn.PairedAnalysis) e escreve um resumo em Markdown + os gráficos de Kaplan-Meier
em `--out-dir`. Fora do CI (§3, "é análise de dado local, não código de produto")."""

from __future__ import annotations

import argparse
from pathlib import Path

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

    summary_path = out_dir / "resumo.md"
    summary_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"Resumo escrito em {summary_path}")


if __name__ == "__main__":
    main()
