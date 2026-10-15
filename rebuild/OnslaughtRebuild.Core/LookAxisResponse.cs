// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Retail's non-linear look-axis response, ported from
/// references/Onslaught/Player.cpp:334-355.
/// </summary>
/// <remarks>
/// The released law, gated on exactly the four look buttons
/// (BUTTON_MECH_YAW_LEFT/RIGHT, BUTTON_MECH_PITCH_UP/DOWN):
/// <code>
///     t1 = tan(val * 1.2f) * 3.0f          // sign-preserving
///     static t2 = tan(1.2f) * 3.0f
///     val = t1 / t2
/// </code>
/// The 3.0 cancels, so the mapping is tan(1.2*val) / tan(1.2). It is
/// NORMALISED — f(0) = 0 and f(1) = 1 — and therefore COMPRESSIVE, not
/// expansive: the slope at centre is 1.2 / tan(1.2) = 0.4665, so retail gives
/// roughly half our sensitivity to a small correction and reaches full rate
/// only at the stops. The developers' comment beside it is the specification:
/// "should give a curve so 50% before would result in 25% after"; the law
/// returns 0.266 at 0.5.
///
/// Core is integer fixed-point and must stay bit-reproducible, so the curve is
/// a checked-in table rather than a Math.Tan call — .NET does not guarantee
/// transcendentals are identical across runtimes, and the smoke state hash
/// would inherit that.
///
/// The table holds every representable input, so nothing is interpolated and
/// the response is the NEAREST INTEGER PERMILLE to the law at all 1001 of them.
/// The worst case is therefore 0.4997 permille, at input 348, where the law is
/// 172.5003 and no integer is closer. That is the floor for any scheme whose
/// output is an integer permille — it cannot be improved on, only matched.
/// LookAxisResponseTests bounds the table at ±0.5 rather than the ±1.0 it used
/// while interpolating, and separately pins every entry to the nearest integer,
/// so no edit to any entry survives: the smallest one available, a single
/// permille at input 348, lands at 0.5003 and fails both.
///
/// It was a 101-entry table sampled every 10 permille and interpolated until
/// 2026-07-31. That cost 0.944 permille at input 985 — 94 % of the bound, so a
/// table edit worth a tenth of a permille breached the gate. Interpolation was
/// the whole of that error, and refining it does not fix it: measured over all
/// 1001 inputs, the best integer nodes obtainable at 10-permille spacing still
/// cost 0.858, and at 5, 4 and 2 permille (201, 251 and 501 entries) 0.805,
/// 0.751 and 0.736. Linear interpolation of a convex law leaves a chord gap
/// that node placement cannot remove, so dropping interpolation was the only
/// route under 0.5.
///
/// Retail has no digital look producer at all — PCController.cpp:91-95 maps all
/// four look buttons to ANALOGUE_X2/Y2. Core's digital +-1 saturates the
/// combined input, and the curve maps full deflection to full deflection, so a
/// digital tap stays faithful; only partial analog values were diverging.
/// </remarks>
public static class LookAxisResponse
{
    private const int Step = 1;

    // round(1000 * tan(1.2 * m/1000) / tan(1.2)) for m in 0..1000.
    private static readonly int[] Curve =
    [
        0, 0, 1, 1, 2, 2, 3, 3, 4, 4,
        5, 5, 6, 6, 7, 7, 7, 8, 8, 9,
        9, 10, 10, 11, 11, 12, 12, 13, 13, 14,
        14, 14, 15, 15, 16, 16, 17, 17, 18, 18,
        19, 19, 20, 20, 21, 21, 21, 22, 22, 23,
        23, 24, 24, 25, 25, 26, 26, 27, 27, 28,
        28, 29, 29, 29, 30, 30, 31, 31, 32, 32,
        33, 33, 34, 34, 35, 35, 36, 36, 36, 37,
        37, 38, 38, 39, 39, 40, 40, 41, 41, 42,
        42, 43, 43, 44, 44, 45, 45, 45, 46, 46,
        47, 47, 48, 48, 49, 49, 50, 50, 51, 51,
        52, 52, 53, 53, 54, 54, 54, 55, 55, 56,
        56, 57, 57, 58, 58, 59, 59, 60, 60, 61,
        61, 62, 62, 63, 63, 64, 64, 64, 65, 65,
        66, 66, 67, 67, 68, 68, 69, 69, 70, 70,
        71, 71, 72, 72, 73, 73, 74, 74, 75, 75,
        76, 76, 77, 77, 78, 78, 78, 79, 79, 80,
        80, 81, 81, 82, 82, 83, 83, 84, 84, 85,
        85, 86, 86, 87, 87, 88, 88, 89, 89, 90,
        90, 91, 91, 92, 92, 93, 93, 94, 94, 95,
        95, 96, 96, 97, 97, 98, 98, 99, 99, 100,
        100, 101, 101, 102, 102, 103, 103, 104, 104, 105,
        105, 106, 106, 107, 107, 108, 108, 109, 109, 110,
        110, 111, 111, 112, 112, 113, 113, 114, 114, 115,
        115, 116, 116, 117, 117, 118, 118, 119, 119, 120,
        120, 121, 121, 122, 122, 123, 123, 124, 124, 125,
        125, 126, 126, 127, 127, 128, 128, 129, 130, 130,
        131, 131, 132, 132, 133, 133, 134, 134, 135, 135,
        136, 136, 137, 137, 138, 138, 139, 139, 140, 141,
        141, 142, 142, 143, 143, 144, 144, 145, 145, 146,
        146, 147, 147, 148, 148, 149, 150, 150, 151, 151,
        152, 152, 153, 153, 154, 154, 155, 155, 156, 157,
        157, 158, 158, 159, 159, 160, 160, 161, 161, 162,
        163, 163, 164, 164, 165, 165, 166, 166, 167, 167,
        168, 169, 169, 170, 170, 171, 171, 172, 173, 173,
        174, 174, 175, 175, 176, 176, 177, 178, 178, 179,
        179, 180, 180, 181, 182, 182, 183, 183, 184, 184,
        185, 186, 186, 187, 187, 188, 188, 189, 190, 190,
        191, 191, 192, 192, 193, 194, 194, 195, 195, 196,
        197, 197, 198, 198, 199, 199, 200, 201, 201, 202,
        202, 203, 204, 204, 205, 205, 206, 207, 207, 208,
        208, 209, 210, 210, 211, 211, 212, 213, 213, 214,
        214, 215, 216, 216, 217, 217, 218, 219, 219, 220,
        221, 221, 222, 222, 223, 224, 224, 225, 226, 226,
        227, 227, 228, 229, 229, 230, 231, 231, 232, 232,
        233, 234, 234, 235, 236, 236, 237, 238, 238, 239,
        239, 240, 241, 241, 242, 243, 243, 244, 245, 245,
        246, 247, 247, 248, 249, 249, 250, 251, 251, 252,
        252, 253, 254, 254, 255, 256, 256, 257, 258, 259,
        259, 260, 261, 261, 262, 263, 263, 264, 265, 265,
        266, 267, 267, 268, 269, 269, 270, 271, 271, 272,
        273, 274, 274, 275, 276, 276, 277, 278, 278, 279,
        280, 281, 281, 282, 283, 283, 284, 285, 286, 286,
        287, 288, 288, 289, 290, 291, 291, 292, 293, 294,
        294, 295, 296, 297, 297, 298, 299, 299, 300, 301,
        302, 302, 303, 304, 305, 305, 306, 307, 308, 309,
        309, 310, 311, 312, 312, 313, 314, 315, 315, 316,
        317, 318, 319, 319, 320, 321, 322, 322, 323, 324,
        325, 326, 326, 327, 328, 329, 330, 330, 331, 332,
        333, 334, 334, 335, 336, 337, 338, 339, 339, 340,
        341, 342, 343, 343, 344, 345, 346, 347, 348, 348,
        349, 350, 351, 352, 353, 354, 354, 355, 356, 357,
        358, 359, 360, 360, 361, 362, 363, 364, 365, 366,
        367, 367, 368, 369, 370, 371, 372, 373, 374, 375,
        375, 376, 377, 378, 379, 380, 381, 382, 383, 384,
        385, 386, 386, 387, 388, 389, 390, 391, 392, 393,
        394, 395, 396, 397, 398, 399, 400, 401, 402, 403,
        404, 404, 405, 406, 407, 408, 409, 410, 411, 412,
        413, 414, 415, 416, 417, 418, 419, 420, 421, 422,
        423, 424, 425, 426, 428, 429, 430, 431, 432, 433,
        434, 435, 436, 437, 438, 439, 440, 441, 442, 443,
        444, 445, 447, 448, 449, 450, 451, 452, 453, 454,
        455, 456, 457, 459, 460, 461, 462, 463, 464, 465,
        466, 468, 469, 470, 471, 472, 473, 475, 476, 477,
        478, 479, 480, 482, 483, 484, 485, 486, 488, 489,
        490, 491, 492, 494, 495, 496, 497, 498, 500, 501,
        502, 503, 505, 506, 507, 508, 510, 511, 512, 514,
        515, 516, 517, 519, 520, 521, 523, 524, 525, 527,
        528, 529, 531, 532, 533, 535, 536, 537, 539, 540,
        541, 543, 544, 546, 547, 548, 550, 551, 552, 554,
        555, 557, 558, 560, 561, 562, 564, 565, 567, 568,
        570, 571, 573, 574, 576, 577, 579, 580, 582, 583,
        585, 586, 588, 589, 591, 592, 594, 595, 597, 599,
        600, 602, 603, 605, 607, 608, 610, 611, 613, 615,
        616, 618, 620, 621, 623, 625, 626, 628, 630, 631,
        633, 635, 636, 638, 640, 642, 643, 645, 647, 649,
        650, 652, 654, 656, 658, 659, 661, 663, 665, 667,
        668, 670, 672, 674, 676, 678, 680, 682, 683, 685,
        687, 689, 691, 693, 695, 697, 699, 701, 703, 705,
        707, 709, 711, 713, 715, 717, 719, 721, 723, 725,
        727, 730, 732, 734, 736, 738, 740, 742, 745, 747,
        749, 751, 753, 756, 758, 760, 762, 765, 767, 769,
        771, 774, 776, 778, 781, 783, 785, 788, 790, 793,
        795, 797, 800, 802, 805, 807, 810, 812, 815, 817,
        820, 822, 825, 828, 830, 833, 835, 838, 841, 843,
        846, 849, 851, 854, 857, 859, 862, 865, 868, 871,
        873, 876, 879, 882, 885, 888, 891, 894, 896, 899,
        902, 905, 908, 911, 914, 918, 921, 924, 927, 930,
        933, 936, 939, 943, 946, 949, 952, 956, 959, 962,
        966, 969, 972, 976, 979, 983, 986, 989, 993, 996,
        1000,
    ];

    /// <summary>
    /// Maps a clamped look input in permille to its released response, in
    /// permille. Odd-symmetric, so axis inversion may be applied on either
    /// side of the call.
    /// </summary>
    public static int Apply(int inputPermille)
    {
        int magnitude = Math.Clamp(Math.Abs(inputPermille), 0, 1_000);
        int index = magnitude / Step;
        // The curve rises monotonically, so the interpolation delta is never
        // negative and a half-step bias rounds to nearest. At the current Step
        // of 1 every input lands on a node and this term is identically zero;
        // it is kept, unchanged, so that returning to a coarser table is a
        // one-line change and so the rounding stays correct if that happens.
        int response = index >= Curve.Length - 1
            ? Curve[^1]
            : Curve[index] + ((((Curve[index + 1] - Curve[index]) *
                (magnitude - (index * Step))) + (Step / 2)) / Step);
        return inputPermille < 0 ? -response : response;
    }
}
