#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Authored text-only fixtures for the language-table utility."""

from __future__ import annotations

import json
import os
import struct
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import language_dat_decode as decode


TOOL = Path(__file__).with_name("language_dat_decode.py")


def text_file(text: str) -> bytes:
    """One version-3 entry with no sample name, followed by an empty names pool."""
    pool = (text + "\0").encode("utf-16le")
    return (struct.pack("<III", decode.MAGIC_V3, 3, 1)
            + struct.pack("<III", 7, 0, 2**32 - 1)
            + struct.pack("<I", len(pool)) + pool + struct.pack("<I", 0))


class Utf16TextTests(unittest.TestCase):
    def test_bmp_text_stops_at_nul_and_respects_the_offset(self) -> None:
        data = b"prefix" + "Café\0ignored".encode("utf-16le")
        self.assertEqual("Café", decode._read_utf16z(data, len(b"prefix")))

    def test_a_surrogate_pair_becomes_one_unicode_character(self) -> None:
        text = "Authored 🚀 text"
        self.assertEqual(text, decode._read_utf16z((text + "\0").encode("utf-16le"), 0))

    def test_the_limit_still_counts_utf16_code_units(self) -> None:
        data = "🚀Z\0".encode("utf-16le")
        self.assertEqual("🚀", decode._read_utf16z(data, 0, max_chars=2))
        self.assertEqual(chr(0xD83D), decode._read_utf16z(data, 0, max_chars=1))
        self.assertEqual("", decode._read_utf16z(data, 0, max_chars=0))

    def test_unpaired_code_units_keep_their_existing_values(self) -> None:
        for text in (chr(0xD800), chr(0xDC00), chr(0xD800) + "A", chr(0xDC00) + "A"):
            with self.subTest(text=repr(text)):
                data = (text + "\0").encode("utf-16le", errors="surrogatepass")
                self.assertEqual(text, decode._read_utf16z(data, 0))

    def test_an_incomplete_final_code_unit_is_still_ignored(self) -> None:
        self.assertEqual("A", decode._read_utf16z("A".encode("utf-16le") + b"B", 0))


class TextOutputTests(unittest.TestCase):
    def run_tool(self, output: str) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory(prefix="authored-language-") as temporary:
            path = Path(temporary) / "text-only.dat"
            path.write_bytes(text_file("Authored 🚀 text"))
            return subprocess.run(
                [sys.executable, str(TOOL), str(path), "--no-stf", output],
                env={**os.environ, "PYTHONIOENCODING": "utf-8"},
                capture_output=True, text=True, encoding="utf-8", check=False,
            )

    def test_json_can_emit_valid_supplementary_unicode(self) -> None:
        result = self.run_tool("--dump-json")
        self.assertEqual(0, result.returncode, result.stderr)
        row = json.loads(result.stdout)["rows"][0]
        self.assertEqual("Authored 🚀 text", row["text"])
        self.assertIsNone(row["audio"])

    def test_tsv_can_emit_valid_supplementary_unicode(self) -> None:
        result = self.run_tool("--dump-tsv")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("Authored 🚀 text", result.stdout.splitlines()[1].split("\t")[-1])


if __name__ == "__main__":
    unittest.main()
