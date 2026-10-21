#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Check the live Host's whole-game flow (HOST_LIVE=flow) against its straight reference.

For each route (the tapes the Parity route tests prove against the port), run the Host twice under godot-headless:
HOST_FLOW_STRAIGHT=1, the game straight through on Godot's thread, and the stepped run (the game on its own thread,
advanced one Present per step, HOST_DRAW=0). Both write one FlowSnapshot line per Present; they must be identical.
The trace is also held to the port's references where they have the field at that Present: rand()'s count and holdrand
(crt-per-present.txt or the probes' CRT), the front end's EVT and FE line (port-probes-every1-with-FE.txt), and in a
level the nine probe fields (port-probes-every1.txt). References come from ONSLAUGHT_PORT_REFS (the route tests' root).
"""

from __future__ import annotations

import argparse
import filecmp
import os
import subprocess
import sys
from pathlib import Path

HOST = Path(__file__).resolve().parent.parent / "OnslaughtRebuild.Host"

# (name, tape, the port's reference directory, end Present, saves as (card name, file), full boot)
ROUTES = [
    ("fe-r1", "fe-r1-quit-port/fer1.tape", "fe-r1-quit-port", 2000, [], False),
    ("fe-r2", "fe-r2-idle-port/empty.tape", "fe-r2-idle-port", 3500, [], False),
    ("fe-r3", "fe-r3-load-port/fer3b.tape", "fe-r3-load-port", 1600, [("BEA 1", "fe-r3-load-port/BEA 1.bes")], False),
    ("fe-r4", "fe-r4-options-port/feopt.tape", "fe-r4-options-port", 3000, [], False),
    ("fe-r5", "fe-r5-fullboot-idle-port/empty.tape", "fe-r5-fullboot-idle-port", 5700, [], True),
    ("fe-lose", "tapes/fe-level100-lose-quit-7300f.tape", "fe-lose-port", 7300, [], False),
    ("fe-god", "fe-god-port-20261021/fe-god-route.tape", "fe-god-port-20261021", 5200,
     [("Maladim", "fe-god-port-20261021/Maladim.bes")], False),
]


# Known, documented Core-against-port differences (open items, not the Host's): route -> (field, first Present, last Present,
# port minus ours). fe-lose: from Present 4280 to the level's end the port has processed exactly one more event than the
# Core (EVENT_MANAGER's processed count); every other field agrees. Its route test compares rand() and the pages only.
KNOWN = {
    "fe-lose": ("EVN", 4280, 6850, 1),
}


def port_fields(route_dir: Path) -> dict[tuple[int, str], str]:
    """The port's per-Present fields for a route: CRT always; EVT and FE in the front end; the nine in a level."""
    fields: dict[tuple[int, str], str] = {}
    for name in ("crt-per-present.txt", "port-probes-every1.txt", "port-probes-every1-with-FE.txt"):
        path = route_dir / name
        if not path.exists():
            continue
        for line in path.open():
            f = line.split()
            if len(f) == 4 and f[0] == "P" and f[2] not in ("ELD", "ELDEXIT"):
                fields[(int(f[1]), f[2])] = f[3]
    return fields


def against_port(trace: Path, port: dict[tuple[int, str], str], known=None) -> tuple[int, str | None]:
    """
    Compares each trace field the port also has at that Present; returns the count compared and the first difference
    that is not the route's known one (which must then hold exactly: that field, that range, that offset).
    """
    compared = 0
    known_seen = set()
    for line in trace.open():
        f = line.split()
        present, segment = int(f[1]), f[2]
        ours = dict(zip(f[3::2], f[4::2]))
        for key, value in ours.items():
            # The front end's EVT is EVENT_MANAGER's time; the port's EVT probe is the same field there.
            want = port.get((present, key))
            if want is None:
                continue
            compared += 1
            if want != value:
                if known and key == known[0] and known[1] <= present <= known[2] and \
                        int.from_bytes(bytes.fromhex(want), "little") - int.from_bytes(bytes.fromhex(value), "little") == known[3]:
                    known_seen.add(present)
                    continue
                return compared, f"Present {present} ({segment}) {key}: port {want}, host {value}"
    if known and (not known_seen or min(known_seen) != known[1] or max(known_seen) != known[2]):
        span = f"{min(known_seen)}..{max(known_seen)}" if known_seen else "nothing"
        return compared, f"the known {known[0]} difference covered {span}, not {known[1]}..{known[2]}"
    return compared, None


def run_host(env: dict[str, str], log: Path) -> int:
    with log.open("w") as out:
        return subprocess.run(["godot-headless"], cwd=HOST, env=env, stdout=out, stderr=subprocess.STDOUT).returncode


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path, help="a new directory for the traces and logs")
    parser.add_argument("--only", nargs="*", help="route names to run (default: all)")
    args = parser.parse_args()
    refs = Path(os.environ["ONSLAUGHT_PORT_REFS"])
    args.out.mkdir(parents=True, exist_ok=False)
    failures = []
    for name, tape, ref_dir, end, saves, full_boot in ROUTES:
        if args.only and name not in args.only:
            continue
        base = {k: v for k, v in os.environ.items() if not k.startswith("HOST_")}
        base.update(HOST_LIVE="flow", HOST_TAPE=str(refs / tape), HOST_END_PRESENT=str(end), HOST_DRAW="0")
        if saves:
            base["HOST_SAVES"] = ";".join(f"{card}={refs / file}" for card, file in saves)
        if full_boot:
            base["HOST_FULL_BOOT"] = "1"
        straight, stepped = args.out / f"{name}.straight.trace", args.out / f"{name}.stepped.trace"
        a = run_host(dict(base, HOST_FLOW_STRAIGHT="1", HOST_TRACE=str(straight)), args.out / f"{name}.straight.log")
        b = run_host(dict(base, HOST_TRACE=str(stepped)), args.out / f"{name}.stepped.log")
        lines = sum(1 for _ in straight.open()) if straight.exists() else 0
        same = straight.exists() and stepped.exists() and filecmp.cmp(straight, stepped, shallow=False)
        # A route either reaches its end Present or the game quits first (R1's and R4's Quit), in both runs alike.
        quit_first = "result=exit" in (args.out / f"{name}.straight.log").read_text()
        compared, first = against_port(straight, port_fields(refs / ref_dir), KNOWN.get(name)) if straight.exists() else (0, "no trace")
        ok = a == 0 and b == 0 and same and (lines >= end - 3 or quit_first) and first is None and compared > 0
        print(f"{name}: straight exit {a}, stepped exit {b}, {lines} Presents{' (the game quit)' if quit_first else ''}, "
              f"traces {'identical' if same else 'DIFFER'}; port fields compared {compared}, first difference {first or 'none'}")
        if not ok:
            failures.append(name)
    print("HOST_FLOW_CHECK " + ("PASS" if not failures else "FAIL " + " ".join(failures)))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
