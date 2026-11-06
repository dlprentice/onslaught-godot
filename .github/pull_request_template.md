## What changed and why

<!-- One subject per pull request. Say what changes in behaviour. -->

## Evidence

<!-- The capture or test that shows the difference, or the file and line in the pinned Onslaught source or in
     onslaught-decomp that the change follows. A guess about what the original did is not evidence. -->

## Checks

There is no hosted CI. List the exact commands you ran and what they printed (pass, fail, skipped).

- [ ] `python tools/public_build.py test --filter ...` for the affected authored tests (simulation or Host changes)
- [ ] `npm run build` or `python tools/public_build.py build` (Host changes)
- [ ] `npm run test:docs` (or `python3 tools/md_link_check.py --check-only` and `python3 tools/doc_header_check.py`)
- [ ] `npm run test:tools` (tool changes)
- [ ] `npm run test:safety` (new files)

Checks I could not run, and why (for example, no recorded references or no copy of the game):

<!-- A maintainer runs the reference-bearing checks on the proposed commit before a simulation or Host change merges. -->

## Boundaries

- [ ] I added no game files or anything derived from them: executables, data, text dumps, saves (beyond the one tracked
      fixture), captures, byte dumps, decompiler output or instruction listings.
- [ ] I license this contribution under GPL-3.0-only, like the rest of this repository, and I have the right to do so.
- [ ] I added no hosted CI or workflow files.
