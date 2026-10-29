# onslaught-godot: agent guide

Status: active
Last updated: 2026-10-29 (sandboxed builds need a writable /tmp; windowed-run wording made generic; machine-specific folders and tool names removed)
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
- **Windowed runs:** runs that open a window (Host runs and captures, the earlier launcher's `capture` and `run`) never
  open on a visible desktop: they go through an offscreen wrapper that renders on a hidden output, or through the engine's
  headless mode. The wrapper, its flags, the engine build and the capture folders are the machine's own and are set in the
  ignored `local-data/rebuild-env.sh`; for example
  `cd rebuild/OnslaughtRebuild.Host && HOST_PRESENT=150 HOST_CAPTURE=<capture file> <offscreen wrapper> --path .
  --qa local-data/qa/run --pass HOST_ --timeout 600 --done-marker '^HOST_CAPTURED' --done-grace 20 -- --audio-driver Dummy`.
  The earlier launcher's retail inputs come from an ignored lab folder named by `BEA_LOCAL_LAB`; worktrees reuse it,
  never a copy.
- **Sandboxed builds:** MSBuild's worker nodes open named pipes in the temporary folder. In a sandbox whose `/tmp` is
  read-only every worker aborts (SIGABRT, one core dump each). Give a sandboxed build a writable private `/tmp` (or
  `TMPDIR`), or build with `-m:1 -nodeReuse:false -p:UseSharedCompilation=false`.
- **Done means:** the smallest gate that could falsify the change passed (docs: `npm run test:docs`; tools: the
  affected `_tests.py`; rebuild code: the affected Parity tests, then the Parity suite and the Host build).
