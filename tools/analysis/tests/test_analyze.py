from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ANALYSIS = Path(__file__).resolve().parent.parent
FIXTURES = ANALYSIS / "fixtures"


class AnalyzeSmokeTests(unittest.TestCase):
    def test_fixtures_run_end_to_end_without_dropping_every_row(self) -> None:
        # As fixtures têm as mesmas chaves de execução que labeled-runs.sample.csv; se deixarem de ter,
        # o filtro de execuções válidas esvazia a entrada e o analyze.py falha em vez de relatar NaN.
        with tempfile.TemporaryDirectory() as out_dir:
            completed = subprocess.run(
                [sys.executable, str(ANALYSIS / "analyze.py"),
                 "--labeled", str(FIXTURES / "labeled-runs.sample.csv"),
                 "--paired", str(FIXTURES / "paired-analysis.sample.csv"),
                 "--decisions", str(FIXTURES / "decisions.sample.csv"),
                 "--loop-latency", str(FIXTURES / "loop-latency.sample.csv"),
                 "--mttd", str(FIXTURES / "mttd.sample.csv"),
                 "--out-dir", out_dir],
                capture_output=True, text=True, encoding="utf-8",
            )
            self.assertEqual(completed.returncode, 0, completed.stderr)
            summary = (Path(out_dir) / "resumo.md").read_text(encoding="utf-8")

        self.assertIn("_decisions: 9 linhas de execuções válidas; 0 linhas de 0 execuções descartadas removidas._", summary)
        self.assertIn("_mttd: 4 linhas de execuções válidas", summary)
        self.assertNotIn("nan [nan", summary)


if __name__ == "__main__":
    unittest.main()
