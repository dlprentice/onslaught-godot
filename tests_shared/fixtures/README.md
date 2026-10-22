# Shared Test Fixtures

Status: active — the one tracked save fixture and its rules
Last updated: 2026-10-21 (header added)
Summary: what `gold_career_save.bin` is, why it is the one tracked save, and how tests must use it.

- `gold_career_save.bin` is the immutable 10,004-byte baseline used by C# and
  Python regression tests.
- It is copied from a real game-generated 10,004-byte save so tests can validate retail save semantics without depending on the private `save-attempts/` tree.
- This is a narrow public-primary exception for one regression fixture. It is
  not permission to commit arbitrary `.bes`, `.bea`, options, or save-attempt
  payloads.
- Treat this fixture as read-only. Tests should copy it to temporary `.bes` or
  `.bea` paths before writing.
