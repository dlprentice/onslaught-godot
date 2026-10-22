// SPDX-License-Identifier: GPL-3.0-only
// C3DGamut: the cells of the map the camera can see. Adapted from bea-decomp src/gcgamut.cpp (ComputePlanes 004742a0,
// MakeIt 00476a20).

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Render;

/// <summary>
/// The map cut into 64 x 64 cells of 8; each render MakeIt bounds the cells under the view and gives each the height
/// span the view's four side planes leave open (in 60/127ths of a unit); a cell whose span is open is visible.
/// </summary>
public sealed class Gamut
{
    public const int CellSize = 8;
    public const int GridSize = 64;

    /// <summary>mSpans: each cell's (high, low).</summary>
    public readonly sbyte[] Spans = new sbyte[GridSize * GridSize * 2];

    public readonly byte[] Visible = new byte[GridSize * GridSize];

    /// <summary>Diagnostic: each cell's unquantised low and high heights from the last MakeIt.</summary>
    public readonly float[] RawLow = new float[GridSize * GridSize], RawHigh = new float[GridSize * GridSize];

    public int MinX, MinY, MaxX, MaxY;

    public FVector CameraPos;

    /// <summary>The span of cell (x, y) is open.</summary>
    public bool Open(int x, int y) => Spans[((y * GridSize) + x) * 2] >= Spans[(((y * GridSize) + x) * 2) + 1];

    /// <summary>MakeIt: the apex (half a cell back) and the far corners (the second and fourth a tenth further out).</summary>
    /// <summary>Diagnostic: the last MakeIt's arguments.</summary>
    public (FMatrix View, float Far, float ZoomX, float ZoomY, FVector Pos) LastInputs;

    public void MakeIt(FMatrix view, float farDistance, float zoomx, float zoomy, FVector pos)
    {
        LastInputs = (view, farDistance, zoomx, zoomy, pos);
        CameraPos = pos;
        float fx = farDistance * zoomx;
        float fy = farDistance * zoomy;
        float nfar = farDistance + 0.1f;
        var points = new FVector[5];
        points[0] = new FVector(pos.X - 4.0f, pos.Y - 4.0f, pos.Z);
        points[1] = (view.Row2 * farDistance) + points[0] + (view.Row0 * fx) + (view.Row1 * fy);
        points[2] = (view.Row2 * nfar) + points[0] + (view.Row0 * fx) - (view.Row1 * fy);
        points[3] = (view.Row2 * farDistance) + points[0] - (view.Row0 * fx) - (view.Row1 * fy);
        points[4] = (view.Row2 * nfar) + points[0] - (view.Row0 * fx) + (view.Row1 * fy);
        for (int i = 0; i < 5; i++)
        {
            points[i].X /= CellSize;
            points[i].Y /= CellSize;
        }

        MinX = MinY = 12340;
        MaxX = MaxY = -12340;
        for (int i = 0; i < 5; i++)
        {
            int x = X87.Fist(points[i].X);
            MaxX = Math.Max(MaxX, x);
            MinX = Math.Min(MinX, x);
            int y = X87.Fist(points[i].Y);
            MaxY = Math.Max(MaxY, y);
            MinY = Math.Min(MinY, y);
        }

        MaxX = Math.Max(Math.Min(MaxX + 2, GridSize - 1), 0);
        MinX = Math.Min(Math.Max(MinX - 2, 0), GridSize - 1);
        MaxY = Math.Max(Math.Min(MaxY + 2, GridSize - 1), 0);
        MinY = Math.Min(Math.Max(MinY - 2, 0), GridSize - 1);
        ComputePlanes(points);
        int n = 0;
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                Visible[n] = (byte)(x >= MinX && x < MaxX && y >= MinY && y < MaxY && Spans[n * 2] >= Spans[(n * 2) + 1] ? 1 : 0);
                n++;
            }
        }
    }

    /// <summary>
    /// Quantise: (h * 127) / 60 on the x87, converted as /QIfist compiles (SINT): fistp in the current rounding mode, to
    /// nearest; then clamped to a signed byte.
    /// </summary>
    private static sbyte Quantise(float h) => (sbyte)Math.Clamp(X87.Fist(h * 127.0 / 60.0), -127, 127);

    /// <summary>ComputePlanes: each side plane through the apex and two far corners, solved for height over the grid.</summary>
    private void ComputePlanes(FVector[] points)
    {
        FVector[] a = [points[0], points[0], points[0], points[0]];
        FVector[] b = [points[2], points[3], points[4], points[1]];
        FVector[] c = [points[1], points[2], points[3], points[4]];
        var o = new FVector[4];
        for (int i = 0; i < 4; i++)
        {
            FVector nrm = FVector.Cross(c[i] - a[i], b[i] - a[i]);
            nrm.Normalise();
            o[i] = nrm * 1.41f * 0.5f;
        }

        var d = new float[4];
        for (int i = 0; i < 4; i++)
        {
            d[i] = ((c[i].Y - a[i].Y) * (b[i].X - a[i].X)) - ((b[i].Y - a[i].Y) * (c[i].X - a[i].X));
        }

        var dx = new float[4];
        var dy = new float[4];
        var z = new float[4];
        float a0z = a[0].Z;
        for (int i = 0; i < 4; i++)
        {
            // The fourth plane takes the first plane's shifted apex Z in its first differences.
            float za = i == 3 ? a0z : a[i].Z;
            if (d[i] != 0)
            {
                dx[i] = (((b[i].Z - za) * (c[i].Y - a[i].Y)) - ((b[i].Y - a[i].Y) * (c[i].Z - a[i].Z))) / d[i];
                dy[i] = (((b[i].X - a[i].X) * (c[i].Z - za)) - ((b[i].Z - a[i].Z) * (c[i].X - a[i].X))) / d[i];
                a[i] += o[i];
                if (i == 0)
                {
                    a0z = a[0].Z;
                }

                z[i] = a[i].Z - (dx[i] * a[i].X) - (dy[i] * a[i].Y) + (dy[i] * MinY);
            }
            else
            {
                dx[i] = 0;
                dy[i] = 0;
                d[i] = 1.0f;
                z[i] = -99999.0f;
            }
        }

        bool[] low = [d[0] > 0, d[1] > 0, d[2] > 0, d[3] > 0];
        int rows = MaxY - MinY + 1;
        int row = MinY * GridSize * 2;
        for (; rows > 0; rows--)
        {
            float fx = MinX;
            var h = new float[4];
            for (int i = 0; i < 4; i++)
            {
                h[i] = (fx * dx[i]) + z[i];
            }

            int cols = MaxX - MinX + 1;
            int span = row + (MinX * 2);
            for (; cols > 0; cols--)
            {
                float lo = -9999.0f;
                float hi = 9999.0f;
                for (int i = 0; i < 4; i++)
                {
                    if (low[i])
                    {
                        if (h[i] > lo)
                        {
                            lo = h[i];
                        }
                    }
                    else if (h[i] < hi)
                    {
                        hi = h[i];
                    }
                }

                Spans[span + 1] = Quantise(lo);
                Spans[span] = Quantise(hi);
                RawLow[span / 2] = lo;
                RawHigh[span / 2] = hi;
                span += 2;
                for (int i = 0; i < 4; i++)
                {
                    h[i] += dx[i];
                }
            }

            for (int i = 0; i < 4; i++)
            {
                z[i] += dy[i];
            }

            row += GridSize * 2;
        }
    }
}
