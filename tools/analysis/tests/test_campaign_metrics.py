from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from campaign_metrics import (  # noqa: E402
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
    median_with_ci,
    mttd_summary,
    restoration_per_decision,
)

FIXTURES = Path(__file__).resolve().parent.parent / "fixtures"
DECISIONS = FIXTURES / "decisions.sample.csv"
LOOP_LATENCY = FIXTURES / "loop-latency.sample.csv"
MTTD = FIXTURES / "mttd.sample.csv"


class ExpectedActionRateTests(unittest.TestCase):
    def test_arm_a_itt_matches_reference_in_both_decisions(self) -> None:
        df = load_decisions(DECISIONS)
        table = expected_action_rate(df)

        row = table[(table["braco"] == "A") & (table["protocolo"] == "ITT")].iloc[0]
        self.assertEqual(row["acertos"], 2)
        self.assertEqual(row["total"], 2)

    def test_arm_b_itt_counts_all_five_decisions_three_match(self) -> None:
        # F2 -> ScaleUp (action_type 0) é a referência. Das 5 decisões do braço B na fixture
        # (plans 2,3,4,7,9), batem 2,4,9 (ScaleUp) — 3 de 5.
        df = load_decisions(DECISIONS)
        table = expected_action_rate(df)

        row = table[(table["braco"] == "B") & (table["protocolo"] == "ITT")].iloc[0]
        self.assertEqual(row["acertos"], 3)
        self.assertEqual(row["total"], 5)

    def test_arm_b_per_protocol_excludes_decisions_with_llm_failure(self) -> None:
        # Só plans 2 e 4 têm n_failure_reasons=0 — os dois batem com ScaleUp.
        df = load_decisions(DECISIONS)
        table = expected_action_rate(df)

        row = table[(table["braco"] == "B") & (table["protocolo"] == "por_protocolo")].iloc[0]
        self.assertEqual(row["acertos"], 2)
        self.assertEqual(row["total"], 2)

    def test_arm_c_itt_one_of_two_matches(self) -> None:
        df = load_decisions(DECISIONS)
        table = expected_action_rate(df)

        row = table[(table["braco"] == "C") & (table["protocolo"] == "ITT")].iloc[0]
        self.assertEqual(row["acertos"], 1)
        self.assertEqual(row["total"], 2)


class EffectiveActionRateTests(unittest.TestCase):
    def test_arm_a_never_executed_reports_zero_denominator(self) -> None:
        # Modo Observe (ADR-05): braço A nunca chega a HealingOutcome nenhum.
        df = load_decisions(DECISIONS)
        table = effective_action_rate(df)

        row = table[(table["braco"] == "A") & (table["protocolo"] == "ITT")].iloc[0]
        self.assertEqual(row["executadas"], 0)

    def test_arm_b_itt_excludes_noop_and_rejected_rows_as_not_executed(self) -> None:
        # Executadas de verdade (Succeeded/PartiallyApplied/Failed, e não-NoOp): plans 2, 4 e 9 —
        # 3 no total. Plan 3 é NoOp com outcome real (Succeeded/false, o comportamento verdadeiro
        # do HealingActionExecutor — achado do code-reviewer: NoOp sempre sai Succeeded, nunca sem
        # outcome, então a fixture precisa modelar isso pra testar a exclusão de verdade) e plan 7
        # saiu Rejected — nenhum dos dois é "executado".
        df = load_decisions(DECISIONS)
        table = effective_action_rate(df)

        row = table[(table["braco"] == "B") & (table["protocolo"] == "ITT")].iloc[0]
        self.assertEqual(row["executadas"], 3)
        # Restauraram: plan 2 (true) e plan 9 (true); plan 4 é false.
        self.assertEqual(row["restauradas"], 2)

    def test_noop_with_real_succeeded_outcome_is_never_counted_as_executed(self) -> None:
        # Regressão do achado do code-reviewer: HealingActionExecutor trata NoOp como curto-circuito
        # e sempre devolve Succeeded/sloRestored=false, mesmo sem tocar o cluster. Plan 6 (braço C)
        # modela exatamente isso — sem o filtro por action_name, contaria como "executada" e
        # inflaria o denominador com uma ação que nunca teve efeito real.
        df = load_decisions(DECISIONS)
        table = effective_action_rate(df)

        row = table[(table["braco"] == "C") & (table["protocolo"] == "ITT")].iloc[0]
        # Só plan 5 (ScaleUp/Succeeded) conta — plan 6 (NoOp/Succeeded) fica de fora.
        self.assertEqual(row["executadas"], 1)

    def test_arm_b_per_protocol_gives_different_rate_than_itt(self) -> None:
        # Por protocolo (n_failure_reasons==0 E executada): só plans 2 e 4 — restaura 1 de 2 (50%),
        # diferente do ITT (2 de 3, ~66,7%) — prova que a distincao ITT/por-protocolo importa aqui.
        df = load_decisions(DECISIONS)
        table = effective_action_rate(df)

        itt = table[(table["braco"] == "B") & (table["protocolo"] == "ITT")].iloc[0]
        pp = table[(table["braco"] == "B") & (table["protocolo"] == "por_protocolo")].iloc[0]

        self.assertEqual(pp["executadas"], 2)
        self.assertEqual(pp["restauradas"], 1)
        self.assertNotAlmostEqual(itt["taxa"], pp["taxa"])

    def test_arm_c_one_execution_fully_restored(self) -> None:
        df = load_decisions(DECISIONS)
        table = effective_action_rate(df)

        row = table[(table["braco"] == "C") & (table["protocolo"] == "ITT")].iloc[0]
        self.assertEqual(row["executadas"], 1)
        self.assertEqual(row["restauradas"], 1)


class FallbackReasonsTests(unittest.TestCase):
    def test_decomposes_stacked_reasons_across_five_arm_b_decisions(self) -> None:
        # Timeout aparece em plans 3, 7 e 9 (3 de 5 decisoes do braco B); InvalidJson só no plan 7.
        df = load_decisions(DECISIONS)
        table = fallback_reasons_table(df)

        timeout_row = table[table["motivo"] == "Timeout"].iloc[0]
        invalid_json_row = table[table["motivo"] == "InvalidJson"].iloc[0]

        self.assertEqual(timeout_row["ocorrencias"], 3)
        self.assertAlmostEqual(timeout_row["pct_das_decisoes_b"], 3 / 5)
        self.assertEqual(invalid_json_row["ocorrencias"], 1)


class LoopLatencyTests(unittest.TestCase):
    def test_stage_medians_match_hand_computed_values(self) -> None:
        df = load_loop_latency(LOOP_LATENCY)
        summary = loop_latency_summary(df)

        detection = summary[summary["etapa"] == "deteccao_ms"].iloc[0]
        action = summary[summary["etapa"] == "atuacao_ms"].iloc[0]

        # Deteccao (7 linhas, braços B e C juntos): [1000, 2000, 1500, 3000, 2500, 1000, 1000]
        # ordenado [1000, 1000, 1000, 1500, 2000, 2500, 3000] -> mediana 1500.
        self.assertAlmostEqual(detection["mediana_ms"], 1500.0)
        # Atuacao: [5000, 6000, 6000, 7000, 6000, 5000, 5000] ordenado [...] -> mediana 6000.
        self.assertAlmostEqual(action["mediana_ms"], 6000.0)
        self.assertEqual(detection["n"], 7)

    def test_decision_latency_by_arm_separates_llm_from_rule_engine(self) -> None:
        # Braço B (plans 1-5): decisao_ms sempre 3000 (context->plan). Braço C (plans 6-7): 10 e
        # 20ms — decisão quase instantânea do RuleEngine puro. Poolar os dois esconderia essa
        # diferença (achado real da campanha: mediana pooled ficou baixa só porque A+C, RuleEngine,
        # são a maioria das linhas).
        df = load_loop_latency(LOOP_LATENCY)
        by_arm = decision_latency_by_arm(df)

        arm_b = by_arm[by_arm["braco"] == "B"].iloc[0]
        arm_c = by_arm[by_arm["braco"] == "C"].iloc[0]

        self.assertAlmostEqual(arm_b["mediana_ms"], 3000.0)
        self.assertEqual(arm_b["n"], 5)
        self.assertAlmostEqual(arm_c["mediana_ms"], 15.0)
        self.assertEqual(arm_c["n"], 2)

    def test_constant_stage_gives_degenerate_ci_around_same_value(self) -> None:
        # Correlacao é 58000ms nas 5 linhas da fixture (mesma janela de 58s por design) — a mediana
        # bate e o IC bootstrap não pode escapar do único valor observado.
        df = load_loop_latency(LOOP_LATENCY)
        summary = loop_latency_summary(df)

        correlation = summary[summary["etapa"] == "correlacao_ms"].iloc[0]
        self.assertAlmostEqual(correlation["mediana_ms"], 58000.0)
        self.assertAlmostEqual(correlation["ic95_low"], 58000.0)
        self.assertAlmostEqual(correlation["ic95_high"], 58000.0)


class MttdTests(unittest.TestCase):
    def test_per_scenario_median_matches_hand_computed_values(self) -> None:
        df = load_mttd(MTTD)
        summary = mttd_summary(df)

        f1_row = summary[summary["scenario"] == "F1"].iloc[0]
        f2_row = summary[summary["scenario"] == "F2"].iloc[0]
        overall_row = summary[summary["scenario"] == "geral"].iloc[0]

        # F1: onset->sinal em 5s e 2s -> mediana 3.5s.
        self.assertAlmostEqual(f1_row["mttd_mediana_s"], 3.5)
        # F2: 9s e 4s -> mediana 6.5s.
        self.assertAlmostEqual(f2_row["mttd_mediana_s"], 6.5)
        # Geral: [5, 2, 9, 4] ordenado [2, 4, 5, 9] -> mediana 4.5s.
        self.assertAlmostEqual(overall_row["mttd_mediana_s"], 4.5)

    def test_median_with_ci_handles_single_value_without_bootstrap(self) -> None:
        import pandas as pd

        median, low, high, n = median_with_ci(pd.Series([42.0]))
        self.assertEqual(median, 42.0)
        self.assertEqual(n, 1)
        self.assertTrue(pd.isna(low))
        self.assertTrue(pd.isna(high))


class FallbackByScenarioTests(unittest.TestCase):
    def test_counts_decisions_not_stacked_reasons(self) -> None:
        # Braço B na fixture: plans 3, 7 e 9 têm motivo registrado (o 7 tem dois) -> 3 decisões de 5.
        table = fallback_by_scenario(load_decisions(DECISIONS))

        f2 = table[table["scenario"] == "F2"].iloc[0]
        total = table[table["scenario"] == "total"].iloc[0]
        self.assertEqual((f2["com_contingencia"], f2["decisoes"]), (3, 5))
        self.assertEqual((total["com_contingencia"], total["decisoes"]), (3, 5))


class EffectiveActionRateByTypeTests(unittest.TestCase):
    def test_splits_executed_actions_by_type(self) -> None:
        # B: ScaleUp executado nos plans 2 (restaurou), 4 (Failed) e 9 (restaurou); NoOp e Rejected fora.
        table = effective_action_rate_by_type(load_decisions(DECISIONS))

        def row(arm: str, action: str):
            return table[(table["braco"] == arm) & (table["acao"] == action)].iloc[0]

        self.assertEqual((row("B", "ScaleUp")["restauradas"], row("B", "ScaleUp")["executadas"]), (2, 3))
        self.assertEqual((row("C", "ScaleUp")["restauradas"], row("C", "ScaleUp")["executadas"]), (1, 1))
        self.assertEqual(row("B", "RestartPod")["executadas"], 0)


class RestorationPerDecisionTests(unittest.TestCase):
    def test_denominator_is_every_decision_of_the_arm(self) -> None:
        table = restoration_per_decision(load_decisions(DECISIONS))

        b = table[table["braco"] == "B"].iloc[0]
        c = table[table["braco"] == "C"].iloc[0]
        self.assertEqual((b["restauradas"], b["decisoes"]), (2, 5))
        self.assertEqual((c["restauradas"], c["decisoes"]), (1, 2))


if __name__ == "__main__":
    unittest.main()
