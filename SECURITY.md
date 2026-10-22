# Security

Status: active
Last updated: 2026-10-22
Summary: how to report a security problem in the rebuild or its tools, and what they read and write.

## Reporting a problem

Please report security problems privately, not in a public issue: use **Report a vulnerability** on this repository's
Security tab. If that form is not offered, open an issue that asks for a private contact and leaves out the details.
Describe what you found, how to reproduce it, and which commit and system you used.

## What the rebuild touches

The parity core and the host run on your machine and send nothing over the network. They read your own installation
of the game (read only) and the reference recordings you name by environment variable, and write only the files you
name (traces, pictures, recorded tapes), the build output folders and this checkout's ignored `local-data/`
([rebuild/README.md](rebuild/README.md)). Problems that matter most here: a tool or the host writing outside those
places, modifying the game installation it was only meant to read, or reaching the network.
