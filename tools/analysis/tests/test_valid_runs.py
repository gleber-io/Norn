from __future__ import annotations

import sys
import unittest
from pathlib import Path

import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from valid_runs import filter_paired_to_valid_runs, filter_to_valid_runs  # noqa: E402

LABELED = pd.DataFrame({
    "experiment_run_id": ["run-1", "run-2"],
    "run_order": [1, 2],
    "scenario": ["F3", "F3"],
    "arm": ["A", "B"],
})


class FilterToValidRunsTests(unittest.TestCase):
    def test_drops_rows_of_runs_absent_from_labeler_csv(self) -> None:
        # Execução 3 (F3/C) é a descartada: não aparece no CSV do Labeler.
        decisions = pd.DataFrame({"run_order": [1, 2, 3, 3], "scenario": ["F3"] * 4, "arm": ["A", "B", "C", "C"],
                                  "plan_id": ["p1", "p2", "p3", "p4"]})

        filtered = filter_to_valid_runs(decisions, LABELED)

        self.assertEqual(list(filtered["plan_id"]), ["p1", "p2"])

    def test_same_run_order_in_other_scenario_is_not_confused_with_valid_run(self) -> None:
        decisions = pd.DataFrame({"run_order": [1], "scenario": ["F2"], "arm": ["A"]})

        self.assertTrue(filter_to_valid_runs(decisions, LABELED).empty)

    def test_missing_key_column_raises(self) -> None:
        with self.assertRaises(ValueError):
            filter_to_valid_runs(pd.DataFrame({"run_order": [1]}), LABELED)


class FilterPairedToValidRunsTests(unittest.TestCase):
    def test_keeps_only_pairs_of_labeled_runs(self) -> None:
        paired = pd.DataFrame({"experiment_run_id": ["run-2", "run-9", "run-2"], "context_id": ["c1", "c2", "c3"]})

        filtered = filter_paired_to_valid_runs(paired, LABELED)

        self.assertEqual(list(filtered["context_id"]), ["c1", "c3"])


if __name__ == "__main__":
    unittest.main()
