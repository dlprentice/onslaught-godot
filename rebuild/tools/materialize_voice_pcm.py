#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Prepare owned voice PCM with an explicitly supplied, hashed native single-read decoder.

Decoder CLI: INPUT.ogg OUTPUT.pcm. It must perform exactly one COggFileRead::Read(1048576),
under worker CW027f, reporting JSON {decoded_bytes, channels, sample_rate, read_calls, control_word}.
No decoder, library, Ogg or PCM is downloaded or embedded. This tool records supplied provenance;
it does not certify an arbitrary executable as retail-equivalent. Use the admitted native reader.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import resource
import subprocess

CONTRACT = "COggFileRead.Read-1048576-PC53-PCM16LE-v1"
CAP = 1048576


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def hash_value(value):
    if len(value) != 64 or any(c not in "0123456789abcdefABCDEF" for c in value):
        raise ValueError("expected a full SHA-256")
    return value.lower()


def limits():
    resource.setrlimit(resource.RLIMIT_CORE, (0, 0))
    resource.setrlimit(resource.RLIMIT_AS, (512 * 1024 * 1024,) * 2)
    resource.setrlimit(resource.RLIMIT_FSIZE, (CAP,) * 2)
    resource.setrlimit(resource.RLIMIT_CPU, (30, 30))


def prepare(game, output, decoder, decoder_sha, library_sha, reader_sha, voices):
    if sha(decoder) != hash_value(decoder_sha):
        raise ValueError("decoder executable changed")
    library_sha, reader_sha = hash_value(library_sha), hash_value(reader_sha)
    if not voices or len(voices) > 4096:
        raise ValueError("select 1..4096 actual voice paths")
    output.mkdir(parents=True, exist_ok=False)
    entries, seen, total = [], set(), 0
    for path in voices:
        normalized = path.replace("\\", "/")
        parts = normalized.split("/")
        if (any(p in ("", ".", "..") or ":" in p for p in parts)
                or not normalized.lower().startswith("data/sounds/")
                or not normalized.lower().endswith(".ogg") or normalized.lower() in seen):
            raise ValueError("select unique game-relative sound Ogg paths")
        seen.add(normalized.lower())
        source = game.joinpath(*parts)
        if source.stat().st_size > 16 * 1024 * 1024:
            raise ValueError("voice Ogg exceeds preparation input bound")
        ogg_sha = sha(source)
        target = output / f"{len(entries):04d}.pcm"
        result = subprocess.run([str(decoder.resolve()), str(source.resolve()), str(target.resolve())],
                                check=True, capture_output=True, text=True, timeout=35, preexec_fn=limits,
                                env={"PATH": os.environ.get("PATH", "/usr/bin"), "LC_ALL": "C"})
        if len(result.stdout) > 8192 or len(result.stderr) > 8192:
            raise ValueError("unexpected decoder output")
        observation = json.loads(result.stdout)
        size = target.stat().st_size
        if (observation.get("read_calls") != 1 or observation.get("control_word") != "027f"
                or observation.get("channels") != 1 or observation.get("sample_rate") != 44100
                or observation.get("decoded_bytes") != size or not 4 <= size <= CAP or size % 2):
            raise ValueError("decoder did not produce admitted native mono44100 capped PCM16")
        if sha(source) != ogg_sha:
            raise ValueError("source Ogg changed during decoding")
        total += size
        if total > 256 * 1024 * 1024:
            raise ValueError("prepared voice library exceeds its byte bound")
        entries.append({"path": normalized.replace("/", "\\"), "ogg_sha256": ogg_sha,
                        "pcm_file": target.name, "decoded_bytes": size, "pcm_sha256": sha(target)})
    manifest = {"schema": 1, "contract": CONTRACT,
                "decoder": {"executable_sha256": decoder_sha.lower(), "library_sha256": library_sha,
                            "reader_sha256": reader_sha, "control_word": "027f"}, "voices": entries}
    (output / "voices.json").write_text(json.dumps(manifest, indent=2) + "\n")
    return manifest


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--game", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True, help="new ignored/local folder")
    p.add_argument("--decoder", type=Path, required=True)
    p.add_argument("--decoder-sha256", required=True)
    p.add_argument("--library-sha256", required=True)
    p.add_argument("--reader-sha256", required=True)
    p.add_argument("voices", nargs="+")
    a = p.parse_args()
    prepare(a.game, a.output, a.decoder, a.decoder_sha256, a.library_sha256, a.reader_sha256, a.voices)
    print(a.output / "voices.json")


if __name__ == "__main__":
    main()
