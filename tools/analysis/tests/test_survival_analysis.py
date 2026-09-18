"""Testes sobre a fixture sintética — não é validação estatística de dado real da campanha."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from survival_analysis import (  # noqa: E402
    fisher_recovery_rate,
    load_labeled_runs,
    logrank_by_scenario,
    recovery_rate_table,
)

FIXTURE = Path(__file__).resolve().parent.parent / "fixtures" / "labeled-runs.sample.csv"


class LoadLabeledRunsTests(unittest.TestCase):
    def test_load_labeled_runs_reads_all_rows(self) -> None:
        df = load_labeled_runs(FIXTURE)
        self.assertEqual(len(df), 12)

    def test_load_labeled_runs_missing_column_raises(self) -> None:
        import pandas as pd

        bad_df_path = Path(__file__).resolve().parent / "_bad.csv"
        pd.DataFrame({"scenario": ["F1"]}).to_csv(bad_df_path, index=False)
        try:
            with self.assertRaises(ValueError):
                load_labeled_runs(bad_df_path)
        finally:
            bad_df_path.unlink()


class RecoveryRateTableTests(unittest.TestCase):
    def test_recovery_rate_table_matches_expected_fixture_counts(self) -> None:
        df = load_labeled_runs(FIXTURE)
        table = recovery_rate_table(df)

        f1_a = table[(table["scenario"] == "F1") & (table["arm"] == "A")].iloc[0]
        self.assertEqual(f1_a["recovered"], 0)
        self.assertEqual(f1_a["total"], 2)
        self.assertEqual(f1_a["recovery_rate"], 0.0)

        f1_b = table[(table["scenario"] == "F1") & (table["arm"] == "B")].iloc[0]
        self.assertEqual(f1_b["recovered"], 2)
        self.assertEqual(f1_b["recovery_rate"], 1.0)


class LogrankAndFisherTests(unittest.TestCase):
    def test_logrank_by_scenario_returns_overall_and_pairwise_p_values(self) -> None:
        df = load_labeled_runs(FIXTURE)
        result = logrank_by_scenario(df, "F1")

        self.assertIn("overall_p_value", result)
        self.assertIn("A_vs_B_p_value", result)
        self.assertIn("A_vs_C_p_value", result)
        self.assertIn("B_vs_C_p_value", result)
        for value in result.values():
            self.assertGreaterEqual(value, 0.0)
            self.assertLessEqual(value, 1.0)

    def test_fisher_recovery_rate_returns_pairwise_p_values(self) -> None:
        df = load_labeled_runs(FIXTURE)
        result = fisher_recovery_rate(df, "F2")

        self.assertIn("A_vs_B_p_value", result)
        for value in result.values():
            self.assertGreaterEqual(value, 0.0)
            self.assertLessEqual(value, 1.0)


if __name__ == "__main__":
    unittest.main()
