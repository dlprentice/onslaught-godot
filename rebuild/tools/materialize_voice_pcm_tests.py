# SPDX-License-Identifier: GPL-3.0-only
"""Authored decoder-boundary controls; these do not prove native Vorbis PCM equality."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from materialize_voice_pcm import prepare, CONTRACT


class MaterializeVoiceTests(unittest.TestCase):
    def fixture(self, report=None, size=32):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        root = Path(directory.name)
        source = root / "game/data/sounds/english/MessageBox/voice.ogg"
        source.parent.mkdir(parents=True)
        source.write_bytes(b"authored compressed input")
        decoder = root / "decoder"
        report = report or {"decoded_bytes": size, "channels": 1, "sample_rate": 44100,
                            "read_calls": 1, "control_word": "027f"}
        decoder.write_text("#!/usr/bin/python3\nimport sys,json\nfrom pathlib import Path\n"
                           f"Path(sys.argv[2]).write_bytes(bytes({size}))\nprint({json.dumps(json.dumps(report))})\n")
        decoder.chmod(0o700)
        return root, decoder, hashlib.sha256(decoder.read_bytes()).hexdigest()

    def run_prepare(self, root, decoder, digest, voices=None):
        return prepare(root / "game", root / "output", decoder, digest, "1" * 64, "2" * 64,
                       voices or ["data/sounds/english/MessageBox/voice.ogg"])

    def test_binds_actual_source_pcm_decoder_and_single_read_observation(self):
        root, decoder, digest = self.fixture()
        result = self.run_prepare(root, decoder, digest)
        self.assertEqual(result["contract"], CONTRACT)
        self.assertEqual(result["voices"][0]["decoded_bytes"], 32)
        self.assertEqual(result["decoder"]["executable_sha256"], digest)
        self.assertEqual(result["voices"][0]["pcm_sha256"], hashlib.sha256(bytes(32)).hexdigest())

    def test_wrong_decoder_hash_does_not_launch(self):
        root, decoder, _ = self.fixture()
        with self.assertRaises(ValueError):
            self.run_prepare(root, decoder, "0" * 64)
        self.assertFalse((root / "output").exists())

    def test_refuses_uncapped_or_wrong_format_read_receipts(self):
        for field, value in [("decoded_bytes", 30), ("channels", 2), ("sample_rate", 22050),
                             ("read_calls", 2), ("control_word", "007f")]:
            with self.subTest(field=field):
                report = {"decoded_bytes": 32, "channels": 1, "sample_rate": 44100,
                          "read_calls": 1, "control_word": "027f", field: value}
                root, decoder, digest = self.fixture(report)
                with self.assertRaises(ValueError):
                    self.run_prepare(root, decoder, digest)
                self.assertFalse((root / "output/voices.json").exists())

    def test_empty_paths_duplicate_and_escape_are_refused(self):
        for paths in [[], ["../voice.ogg"], ["data/sounds/english/MessageBox/voice.ogg"] * 2]:
            root, decoder, digest = self.fixture()
            with self.assertRaises(ValueError):
                prepare(root / "game", root / "output", decoder, digest, "1" * 64, "2" * 64, paths)

    def test_preexisting_output_is_never_resealed(self):
        root, decoder, digest = self.fixture()
        (root / "output").mkdir()
        with self.assertRaises(FileExistsError):
            self.run_prepare(root, decoder, digest)


if __name__ == "__main__":
    unittest.main()
