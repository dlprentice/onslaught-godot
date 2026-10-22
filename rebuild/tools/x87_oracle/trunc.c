/* SPDX-License-Identifier: GPL-3.0-only
 * Compiled without /QIfist, like retail's cframetimer.cpp: a C cast truncates toward zero (_ftol). */
int trunc_to_int(double a)
{
    return (int)a;
}
