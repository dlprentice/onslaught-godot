/* SPDX-License-Identifier: GPL-3.0-only
 *
 * x87 oracle harness for the rebuild's parity numerics. Built with the pinned VC6 SP5 compiler using the flags
 * retail was built with (/O2 /QIfist), run under Wine with the FPU at 24-bit precision control, as retail's
 * control word 007f sets it. It reads N pairs of doubles and writes, for each pair, the result of one operation as
 * the compiled game code would produce it.
 *
 *   harness.exe <op> <in.bin> <out.bin>
 *
 * in.bin: N records of two little-endian doubles (a, b). out.bin: N results: 8-byte double for the *_d ops,
 * 4-byte float for the *_f ops, 4-byte int for fist and trunc. Ops:
 *   add_d sub_d mul_d div_d      a op b, stored to a double
 *   add_f sub_f mul_f div_f      (float)a op (float)b, stored to a float
 *   sqrt_d sqrt_f                sqrt(a)
 *   sin_d sin_f cos_d cos_f      sin(a), cos(a) (the fsin/fcos intrinsics at /O2)
 *   atan2_d atan2_f              atan2(a, b) (fpatan)
 *   d2f                          (float)a
 *   fist                         (int)a under /QIfist: fistp, round to nearest even
 *   trunc                        (int)a without /QIfist: C truncation (see trunc.c)
 */
#include <float.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

int trunc_to_int(double a); /* trunc.c, compiled without /QIfist */

static double *g_a, *g_b;
static long g_n;

static int load(const char *path)
{
    FILE *f = fopen(path, "rb");
    long size, i;
    double *buf;
    if (!f) return 0;
    fseek(f, 0, SEEK_END);
    size = ftell(f);
    fseek(f, 0, SEEK_SET);
    g_n = size / 16;
    buf = (double *)malloc(size ? size : 16);
    if (fread(buf, 16, g_n, f) != (size_t)g_n) { fclose(f); return 0; }
    fclose(f);
    g_a = (double *)malloc(g_n * 8 + 8);
    g_b = (double *)malloc(g_n * 8 + 8);
    for (i = 0; i < g_n; i++) { g_a[i] = buf[2 * i]; g_b[i] = buf[2 * i + 1]; }
    free(buf);
    return 1;
}

int main(int argc, char **argv)
{
    const char *op;
    FILE *out;
    long i;
    double *rd;
    float *rf;
    int *ri;
    if (argc != 4 || !load(argv[2])) { fprintf(stderr, "usage: harness <op> <in> <out>\n"); return 2; }
    op = argv[1];
    rd = (double *)malloc(g_n * 8 + 8);
    rf = (float *)malloc(g_n * 4 + 4);
    ri = (int *)malloc(g_n * 4 + 4);
    _controlfp(_PC_24, _MCW_PC);
    _controlfp(_RC_NEAR, _MCW_RC);
    _controlfp(_MCW_EM, _MCW_EM);

    if (!strcmp(op, "add_d")) for (i = 0; i < g_n; i++) rd[i] = g_a[i] + g_b[i];
    else if (!strcmp(op, "sub_d")) for (i = 0; i < g_n; i++) rd[i] = g_a[i] - g_b[i];
    else if (!strcmp(op, "mul_d")) for (i = 0; i < g_n; i++) rd[i] = g_a[i] * g_b[i];
    else if (!strcmp(op, "div_d")) for (i = 0; i < g_n; i++) rd[i] = g_a[i] / g_b[i];
    else if (!strcmp(op, "sqrt_d")) for (i = 0; i < g_n; i++) rd[i] = sqrt(g_a[i]);
    else if (!strcmp(op, "sin_d")) for (i = 0; i < g_n; i++) rd[i] = sin(g_a[i]);
    else if (!strcmp(op, "cos_d")) for (i = 0; i < g_n; i++) rd[i] = cos(g_a[i]);
    else if (!strcmp(op, "atan2_d")) for (i = 0; i < g_n; i++) rd[i] = atan2(g_a[i], g_b[i]);
    else if (!strcmp(op, "add_f")) for (i = 0; i < g_n; i++) { float a = (float)g_a[i], b = (float)g_b[i]; rf[i] = a + b; }
    else if (!strcmp(op, "sub_f")) for (i = 0; i < g_n; i++) { float a = (float)g_a[i], b = (float)g_b[i]; rf[i] = a - b; }
    else if (!strcmp(op, "mul_f")) for (i = 0; i < g_n; i++) { float a = (float)g_a[i], b = (float)g_b[i]; rf[i] = a * b; }
    else if (!strcmp(op, "div_f")) for (i = 0; i < g_n; i++) { float a = (float)g_a[i], b = (float)g_b[i]; rf[i] = a / b; }
    else if (!strcmp(op, "sqrt_f")) for (i = 0; i < g_n; i++) rf[i] = (float)sqrt(g_a[i]);
    else if (!strcmp(op, "sin_f")) for (i = 0; i < g_n; i++) rf[i] = (float)sin(g_a[i]);
    else if (!strcmp(op, "cos_f")) for (i = 0; i < g_n; i++) rf[i] = (float)cos(g_a[i]);
    else if (!strcmp(op, "atan2_f")) for (i = 0; i < g_n; i++) rf[i] = (float)atan2(g_a[i], g_b[i]);
    else if (!strcmp(op, "d2f")) for (i = 0; i < g_n; i++) rf[i] = (float)g_a[i];
    else if (!strcmp(op, "fist")) for (i = 0; i < g_n; i++) ri[i] = (int)g_a[i];
    else if (!strcmp(op, "trunc")) for (i = 0; i < g_n; i++) ri[i] = trunc_to_int(g_a[i]);
    else { fprintf(stderr, "unknown op %s\n", op); return 2; }

    out = fopen(argv[3], "wb");
    if (!out) return 3;
    if (op[strlen(op) - 1] == 'd' && op[strlen(op) - 2] == '_') fwrite(rd, 8, g_n, out);
    else if (!strcmp(op, "fist") || !strcmp(op, "trunc")) fwrite(ri, 4, g_n, out);
    else fwrite(rf, 4, g_n, out);
    fclose(out);
    printf("%s %ld control %04x\n", op, g_n, _controlfp(0, 0) & 0xffff);
    return 0;
}
