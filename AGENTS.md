# onslaught-godot: agent guide

Status: active
Last updated: 2026-10-22
Summary: the rules for working in this repository, for people and automated agents alike.

- **What this is:** the C# Godot rebuild of *Battle Engine Aquila*. `rebuild/OnslaughtRebuild.Parity` is the simulation
  owner and `rebuild/OnslaughtRebuild.Host` the presentation owner; Core, Client, Godot and Headless are reference
  only. Setup, the reference environment and the gates are in [rebuild/README.md](rebuild/README.md).
- **Determinism:** the parity core is deterministic given its inputs (the tape, the retail data and options files it
  reads as the game does, and the virtual clock). It never reads wall time, locale, environment or thread scheduling,
  and has no presentation, process, network or GPU dependency ([rebuild/DETERMINISM.md](rebuild/DETERMINISM.md)).
  Tests compare it with recorded references Present by Present; a missing reference, an empty selection or a new
  skip is not a pass.
- **Evidence:** every behaviour claim cites a capture, a byte comparison, a focused test, or a pinned source file and
  line. Static evidence establishes identities and structures; only controlled runtime evidence establishes values.
- **No game files, ever:** never commit the game's executables, data, saves (beyond the one tracked fixture),
  captures, byte dumps, decompiler output or instruction listings. `npm run test:safety` checks the boundary.
- **Code:** C# only, built in code: no `.gd` files, no editor-authored scenes or resources, shader source in C#
  strings. Tooling is Python.
- **Licence:** GPL-3.0-only ([LICENSE](LICENSE), [REUSE.toml](REUSE.toml)); units adapting Stuart Gillam's source are
  recorded in [rebuild/PROVENANCE.md](rebuild/PROVENANCE.md).
- **Machine-specific paths** (reference folders, clones) live in an ignored `local-data/rebuild-env.sh`, never in a
  tracked file. The program sends nothing over the network.
- **Where work happens:** the rebuild is developed here since 2026-10-22, on branches merged into `main` when their
  gate passes; Onslaught-Career-Editor's `rebuild/` is frozen until it is removed. Builds and test runs use a clone on
  an internal disk.
- **Done means:** the smallest gate that could falsify the change passed (docs: `npm run test:docs`; tools: the
  affected `_tests.py`; rebuild code: the affected Parity tests, then the Parity suite and the Host build).
