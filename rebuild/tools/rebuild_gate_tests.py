#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""The rebuild gate's report readers."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import rebuild_gate  # noqa: E402


class RebuildGateReaderTests(unittest.TestCase):
    def test_reads_the_last_dotnet_summary(self) -> None:
        log = ("Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, Duration: 1 s - A.dll\n"
               "Failed!  - Failed:     3, Passed:  1482, Skipped:     2, Total:  1487, Duration: 1 m - B.dll\n")
        self.assertEqual({"failed": 3, "passed": 1482, "skipped": 2, "total": 1487}, rebuild_gate.dotnet_counts(log))
        self.assertIsNone(rebuild_gate.dotnet_counts("no summary"))

    def test_reads_the_replayer_report_after_npm_noise(self) -> None:
        output = ('> npm run\n{\n  "ticks": 2148,\n  "repeats": 2,\n  "traceHash": "aa",\n'
                  '  "finalStateHash": "bb",\n  "traceHashVerified": true,\n  "finalStateHashVerified": true,\n'
                  '  "hull": 20000,\n  "comparison": {"x": 1}\n}\n')
        self.assertEqual({"ticks": 2148, "repeats": 2, "traceHash": "aa", "finalStateHash": "bb",
                          "traceHashVerified": True, "finalStateHashVerified": True, "hull": 20000},
                         rebuild_gate.replay_hashes(output))


if __name__ == "__main__":
    unittest.main()
