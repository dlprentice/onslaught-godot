#!/bin/sh
# fp.sh START STOP: the x87 instructions (and the jumps, compares and integer stores between them) of a retail range.
E="$HOME/.local/share/Steam/steamapps/common/Battle Engine Aquila/BEA.exe"
objdump -d -M intel --start-address=$1 --stop-address=$2 "$E" | sed -n 's/^ *\([0-9a-f]*\):\t[0-9a-f ]*\t\(.*\)/\1 \2/p' | grep -E " (f[a-z]+|j[a-z]+|call|ret|cmp|mov +DWORD PTR \[[a-z]+\+0x[0-9a-f]+\],0x[0-9a-f]{5,})" | sed 's/  */ /g'
