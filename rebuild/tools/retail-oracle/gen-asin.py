#!/usr/bin/env python3
"""Cases for the runtime's asin and acos (seeded): float arguments in [-1, 1] (as the game passes Z / |v| and dot
products), the ends and just past them, and some doubles."""
import random, struct, sys
rnd = random.Random(int(sys.argv[1])); N = int(sys.argv[2])
def f32(x): return struct.unpack('<f', struct.pack('<f', x))[0]
def words(x):
    b = struct.unpack('<Q', struct.pack('<d', x))[0]
    return '%08x %08x' % (b & 0xffffffff, b >> 32)
xs = [1.0, -1.0, 0.0, -0.0, 1 - 2 ** -24, -(1 - 2 ** -24)]  # |x| > 1 and NaN raise the CRT's domain error (thread data the oracle lacks)
for _ in range(N):
    r = rnd.random()
    if r < 0.6: xs.append(f32(rnd.uniform(-1, 1)))
    elif r < 0.8: xs.append(f32(rnd.choice([1, -1]) * (1 - rnd.uniform(0, 1e-3))))
    elif r < 0.9: xs.append(f32(rnd.uniform(-1e-3, 1e-3)))
    else: xs.append(rnd.uniform(-1, 1))
print('\n'.join(f'{fn} {words(x)}' for x in xs for fn in ('asin', 'acos')))
