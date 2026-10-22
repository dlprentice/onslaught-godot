# Retail oracle

Status: active
Last updated: 2026-10-21 (float-consts.py takes the image and executable as inputs)
Summary: runs the retail game's own maths functions natively, as a bit-exact oracle for the parity core's float code.

The parity core must compute every float exactly as retail does: 24-bit precision, and each sum in the order compiled
at its site. Reading those orders from the instructions by hand is slow and error-prone. This tool asks the retail
code itself.

- `mkimage.py BEA.exe OUT` flattens the player's `BEA.exe` (sections at their virtual addresses) into a memory
  image. The image is retail-derived: keep it in an ignored directory, never commit it.
- `retail-oracle.c` is a 32-bit Linux program. It maps the image at the image base 0x400000, so no relocation is
  needed. It sets the x87 control word to 0x007F (the game's: 24-bit precision) around each call, and calls retail
  functions by address. It reads input lines `<function> <hex words...>` and writes `<function> <hex words...>`, with
  floats as IEEE bits. A second argument sets the control word; 0x027F is the negative control, which must differ.
  Build: `gcc -m32 -O1 -no-pie -fno-pic -o retail-oracle retail-oracle.c`.
- `gen-geometry.py SEED N` writes seeded cases for `Collision/Geometry.cs`: constructed hits and near misses, plus
  random cases.
- `gen-buckets.py DIR SEED N` writes poly bucket search cases. DIR holds level 100's buckets as dumped by
  `RetailOracleTests.DumpLevel100PolyBucketsForTheOracle` (set `ONSLAUGHT_RETAIL_ORACLE_DUMP`; retail-derived, keep
  in scratch). Per bucket, N line walks and N sphere searches: a third of the endpoints on cell borders, with
  axis-aligned, exact-diagonal and zero-length lines. `pbload PATH` rebuilds a bucket in retail's layout; `pbline` and
  `pbsphere` list the triangles retail's search hands out, then the search stamp.
- `gen-volume.py DIR SEED N` and `gen-collide.py DIR SEED N` write cases for `Collision/MeshCollisionVolume.cs`.
  The first covers ResolveContact (stuck or not, 1 to 4 planes, half of them facing the travel), the swept sphere
  against a part's box, and against its poly bucket. The second covers CollideWithLine and CollideWithSphere on
  level 100's whole meshes. `meshes PATH` builds those meshes from the dump's `meshes.txt`: the parts' type, number,
  reference, bucket and box. Each case gives every part a transform; the line or sphere aims inside a collision part,
  or sits where BuildStatic puts it. The oracle fakes only what the volume reads: a render thing that answers the
  mesh, a thing without a motion controller, and transforms that are fixed and current, so nothing recaches.
- `gen-asin.py SEED N` writes arguments for VC6's runtime asin and acos (`asin`/`acos`, called with the argument in
  st0). Arguments beyond [-1, 1] and NaN are left out: the runtime reports their domain error through the CRT's thread
  data, which the oracle does not set up.
- `gen-fonts.py DIR SEED` writes cases for the fonts' GetTextExtent and WordWrap. DIR holds the front end's two fonts
  and the language file's texts, dumped by `RetailOracleTests.DumpFrontEndFontsForTheOracle`. `fontload PATH` builds
  a font in CDXBitmapFont's layout with retail's vtable; the first load runs InitCharMap.
- `fp.sh START STOP` lists a retail range's x87 instructions with the jumps, calls and float immediates between them.
  `float-consts.py IMAGE ADDR...` lists each function's x87 reads of memory constants, with their width and value; it
  reads the oracle image IMAGE and disassembles the executable `BEA_EXE` names (default Steam's install). Both are for checking a port's float sites by reading.
- Static initializers: the image holds the storage of dynamically initialized statics as zero. The oracle runs those
  the tested code reads (MeshCollisionVolume.cpp's box corners, 004abf00..004abfe0) before the first case.
- `ONSLAUGHT_RETAIL_ORACLE_OURS=DIR` makes a test write its own outputs, line for line, for diagnosis.

The parity tests read the stored cases and results from `$ONSLAUGHT_RETAIL_ORACLE/<group>/{cases,retail}.txt` and
skip visibly when they are absent (`RetailOracleTests`). The results are retail-derived and stay out of Git, like the
X87 tables.

It suits pure maths, including functions that call other retail code in `.text`, such as the statically linked
CRT. It does not suit code that touches the operating system through the import table. Calling conventions: a cdecl
function is called directly. A function that returns a struct takes MSVC's hidden return pointer as an explicit
first argument and returns it. A `thiscall` method uses gcc's `__attribute__((thiscall))`. Objects are built in the
retail layout, for example the 16-byte `FVector` and the 0xe8-byte `CCollisionReport`.

## Results

| Group | Functions | Cases | Differ |
| --- | --- | --- | --- |
| geometry (seeds 20261015 and 777) | ClipSegmentAgainstAABB3D 00478160, TestSweptSphereAgainstTriangle 00478510, TestSegmentAgainstTriangle 00478c20, PointInsideTrianglePrism 00479020, SelectClosestPointOnTriangleEdges 00479200, RaySphereEntryDistance 00479630, DistanceOutsideBox 00479770 | 7 x 12,000 | 0 (control: 317 to 764 per function) |
| polybucket (level 100's 258 buckets; seeds 1015, 4243, 99) | StartLineSearch 004d50d0 + GetNextPolyLine 004d57c0 (with AdvanceLineSearch, IntersectsCell), StartSearch 004d4c00 + GetNextPoly 004d4f00 | 46,440 searches | 0 (control: 9 on cell-border cases; reversing IntersectsCell fails 2,840) |
| volume (seeds 1016, 77, 4244) | ResolveContact 004acf30, TestSweptSphereAgainstBox 004ac140, TestSweptSphereAgainstPart 004ac4a0 | 3 x 27,000 | 0 (control: 1,112 to 9,094 per function at seed 77) |
| collide (seeds 1017, 4245) | CollideWithLine 004ad830, CollideWithSphere 004ac6e0 on level 100's meshes | 2 x 6,500 | 0 (the general transform at the contact fails 375 of 5,000 spheres; XYZ in the slide test fails 5) |
| fonts (seed 1019; 1,656 language-file texts) | CDXBitmapFont::GetTextExtent 00540680, WordWrap 00465a20 | 3,312 extents, 6,624 wraps | 0 (dropping the per-character +1 fails every extent and 2,580 wraps) |
| asin (seed 1018) | __CIasin 0055dcb0, __CIacos 0055f380 | 2 x 50,006 | 0 as floats and as occupancy's slope; 93 doubles differ in the last bit (fpatan against Math.Atan2). The former float(Math.Asin) + pi/2 slope differed on about 12,300 of 50,006 |
