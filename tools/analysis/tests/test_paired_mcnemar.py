from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from paired_mcnemar import load_paired, mcnemar_result, mcnemar_table, raw_agreement_rate  # noqa: E402

FIXTURE = Path(__file__).resolve().parent.parent / "fixtures" / "paired-analysis.sample.csv"


class PairedMcnemarTests(unittest.TestCase):
    def test_load_paired_reads_all_rows(self) -> None:
        df = load_paired(FIXTURE)
        self.assertEqual(len(df), 7)

    def test_mcnemar_table_matches_expected_fixture_counts(self) -> None:
        df = load_paired(FIXTURE)
        table = mcnemar_table(df)

        # Fixture: 4 pares "ambos certos", 0 "só LLM certo", 2 "só regra certa", 1 "ambos errados".
        self.assertEqual(table, [[4, 0], [2, 1]])

    def test_mcnemar_result_returns_p_value_between_zero_and_one(self) -> None:
        df = load_paired(FIXTURE)
        result = mcnemar_result(df)

        self.assertGreaterEqual(result["p_value"], 0.0)
        self.assertLessEqual(result["p_value"], 1.0)
        self.assertEqual(result["only_llm_correct"], 0)
        self.assertEqual(result["only_rule_correct"], 2)

    def test_raw_agreement_rate_counts_matching_actions(self) -> None:
        df = load_paired(FIXTURE)
        # Concordam em: linhas 1,2 (RestartPod/RestartPod), 3 (ScaleUp/ScaleUp), 6 (Toggle/Toggle),
        # 7 (NoOp/NoOp) = 5 de 7.
        self.assertAlmostEqual(raw_agreement_rate(df), 5 / 7)


if __name__ == "__main__":
    unittest.main()
