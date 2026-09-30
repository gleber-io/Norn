from __future__ import annotations

import sys
import unittest
from pathlib import Path

import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from campaign_metrics import effective_action_rate, load_decisions  # noqa: E402
from cluster_bootstrap import effective_rate_difference_ci, paired_accuracy_difference_ci  # noqa: E402
from paired_mcnemar import load_paired  # noqa: E402

FIXTURES = Path(__file__).resolve().parent.parent / "fixtures"


class PairedAccuracyDifferenceTests(unittest.TestCase):
    def test_point_estimate_pools_all_contexts(self) -> None:
        # Fixture: 7 contextos em 3 execuções; LLM acerta 4, RuleEngine acerta 6 -> (4-6)/7.
        result = paired_accuracy_difference_ci(load_paired(FIXTURES / "paired-analysis.sample.csv"), n_resamples=2000)

        self.assertAlmostEqual(result["diferenca"], -2 / 7)
        self.assertEqual(result["execucoes"], 3)
        self.assertLessEqual(result["ic95_low"], result["diferenca"])
        self.assertGreaterEqual(result["ic95_high"], result["diferenca"])

    def test_same_seed_gives_same_interval(self) -> None:
        df = load_paired(FIXTURES / "paired-analysis.sample.csv")

        self.assertEqual(paired_accuracy_difference_ci(df, n_resamples=500, seed=7),
                         paired_accuracy_difference_ci(df, n_resamples=500, seed=7))

    def test_resamples_whole_runs_not_rows(self) -> None:
        # Uma execução só, com linhas discordantes: reamostrando execuções, toda réplica é a mesma
        # execução e o IC colapsa no ponto; reamostrando linhas, o IC não colapsaria.
        df = pd.DataFrame({"experiment_run_id": ["r1"] * 4, "llm_matches_reference": [1, 0, 1, 0],
                           "rule_matches_reference": [0, 0, 1, 1]})

        result = paired_accuracy_difference_ci(df, n_resamples=500)

        self.assertEqual((result["ic95_low"], result["ic95_high"]), (result["diferenca"], result["diferenca"]))


class EffectiveRateDifferenceTests(unittest.TestCase):
    def test_point_estimate_matches_effective_action_rate_table(self) -> None:
        # A diferença do bootstrap tem de ser a mesma das taxas ITT da tabela — mesmo denominador.
        df = load_decisions(FIXTURES / "decisions.sample.csv")
        table = effective_action_rate(df)
        taxa = {arm: table[(table["braco"] == arm) & (table["protocolo"] == "ITT")].iloc[0]["taxa"] for arm in "BC"}

        result = effective_rate_difference_ci(df, n_resamples=2000)

        self.assertAlmostEqual(result["diferenca"], taxa["C"] - taxa["B"])
        self.assertAlmostEqual(result["diferenca"], 1 - 2 / 3)

    def test_runs_without_executed_action_still_count_as_resampling_units(self) -> None:
        # Fixture: C tem as execuções 9 (ação executada) e 12 (só NoOp); B tem 8 e 11.
        result = effective_rate_difference_ci(load_decisions(FIXTURES / "decisions.sample.csv"), n_resamples=2000)

        self.assertEqual((result["execucoes_C"], result["execucoes_B"]), (2, 2))
        self.assertLessEqual(result["ic95_low"], result["diferenca"])
        self.assertGreaterEqual(result["ic95_high"], result["diferenca"])


if __name__ == "__main__":
    unittest.main()
