// SPDX-License-Identifier: GPL-3.0-or-later
// The Direct3D 9 fixed-function state the front end draws under, from bea-decomp src/State.cpp (CState::UseDefault
// 004eb1e0: alpha blend on, source alpha over; alpha test on, GREATEREQUAL 8; depth test LESSEQUAL with writes),
// src/DXFrontend.cpp (CDXFrontEnd::RenderStart's clear) and src/DXFrontEndVideo.cpp (the video quad's alpha from its
// colour alone).

using OnslaughtRebuild.Parity.Flow.Draw;

namespace OnslaughtRebuild.Host;

/// <summary>A texture's pixels as 0xAARRGGBB, row by row.</summary>
public sealed record RasterTexture(int Width, int Height, uint[] Pixels)
{
    /// <summary>One opaque white texel (the engine's default texture stands in for a video that is not playing).</summary>
    public static readonly RasterTexture White = new(1, 1, [0xffffffff]);
}

/// <summary>
/// Draws the front end's frames as the game's Direct3D state does, in software, into a 640 x 480 picture: each quad's
/// texels (bilinear, wrapping unless clamped) times its colour; texels whose alpha is under 8 are discarded (no colour, no depth); the
/// depth test is LESSEQUAL (or ALWAYS) against a buffer the clear sets to 1, written by every drawn pixel; colour blends
/// source-alpha-over or adds. Pixel centres are at whole coordinates, as in Direct3D 9. No Godot types: the same picture
/// comes out wherever it runs.
/// </summary>
public sealed class FrontEndRaster(Func<QuadTexture, RasterTexture?> textures)
{
    public const int Width = FrontEndDraw.WindowWidth, Height = FrontEndDraw.WindowHeight;

    private readonly float[] _depth = new float[Width * Height];

    /// <summary>The picture, 0xAARRGGBB with alpha 0xff, row by row.</summary>
    public uint[] Pixels { get; } = new uint[Width * Height];

    /// <summary>Draws a frame over what the last one left, after its clear when it has one.</summary>
    public void Draw(CanvasFrame frame)
    {
        if (frame.ClearColour is { } clear)
        {
            Array.Fill(Pixels, 0xff000000 | clear);
            Array.Fill(_depth, 1f);
        }

        foreach (CanvasQuad quad in frame.Quads)
        {
            RasterTexture texture = textures(quad.Texture) ?? RasterTexture.White;
            bool alphaFromColour = quad.Texture.Kind == CanvasTextureKind.Default;
            Triangle(quad, texture, alphaFromColour, quad.V0, quad.V1, quad.V2);
            Triangle(quad, texture, alphaFromColour, quad.V1, quad.V3, quad.V2);
        }
    }

    private void Triangle(in CanvasQuad quad, RasterTexture texture, bool alphaFromColour, CanvasVertex a, CanvasVertex b, CanvasVertex c)
    {
        float area = Edge(a.X, a.Y, b.X, b.Y, c.X, c.Y);
        if (area == 0f || float.IsNaN(area))
        {
            return;
        }

        int x0 = Math.Max(0, (int)MathF.Ceiling(MathF.Min(a.X, MathF.Min(b.X, c.X)) - 0.5f));
        int x1 = Math.Min(Width - 1, (int)MathF.Floor(MathF.Max(a.X, MathF.Max(b.X, c.X)) + 0.5f));
        int y0 = Math.Max(0, (int)MathF.Ceiling(MathF.Min(a.Y, MathF.Min(b.Y, c.Y)) - 0.5f));
        int y1 = Math.Min(Height - 1, (int)MathF.Floor(MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) + 0.5f));
        uint colour = a.Colour;
        float ca = (colour >> 24) / 255f, cr = ((colour >> 16) & 0xff) / 255f, cg = ((colour >> 8) & 0xff) / 255f, cb = (colour & 0xff) / 255f;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float w0 = Edge(b.X, b.Y, c.X, c.Y, x, y) / area;
                float w1 = Edge(c.X, c.Y, a.X, a.Y, x, y) / area;
                float w2 = 1f - w0 - w1;
                // Inside, with a top-left rule so that the quad's shared diagonal covers each pixel once.
                if (!Covers(w0, b, c, area) || !Covers(w1, c, a, area) || !Covers(w2, a, b, area))
                {
                    continue;
                }

                // A quad's corners share their depth: interpolating equal values would round off them, and an equal
                // depth must pass LESSEQUAL (a panel drawn over another at the same depth).
                float z = a.Z == b.Z && b.Z == c.Z ? a.Z : w0 * a.Z + w1 * b.Z + w2 * c.Z;
                int i = y * Width + x;
                if (quad.ZEnable && !quad.ZAlways && !(z <= _depth[i]))
                {
                    continue;
                }

                float u = w0 * a.U + w1 * b.U + w2 * c.U, v = w0 * a.V + w1 * b.V + w2 * c.V;
                uint t = Sample(texture, u, v, quad.Clamp);
                float ta = alphaFromColour ? 1f : (t >> 24) / 255f;
                float sa = ta * ca;
                if (sa * 255f < 7.5f)
                {
                    continue;
                }

                float sr = ((t >> 16) & 0xff) / 255f * cr, sg = ((t >> 8) & 0xff) / 255f * cg, sb = (t & 0xff) / 255f * cb;
                uint d = Pixels[i];
                float dr = ((d >> 16) & 0xff) / 255f, dg = ((d >> 8) & 0xff) / 255f, db = (d & 0xff) / 255f;
                float r, g, bl;
                if (quad.Blend == CanvasBlend.Additive)
                {
                    r = sr + dr;
                    g = sg + dg;
                    bl = sb + db;
                }
                else
                {
                    r = sr * sa + dr * (1f - sa);
                    g = sg * sa + dg * (1f - sa);
                    bl = sb * sa + db * (1f - sa);
                }

                Pixels[i] = 0xff000000 | (Byte(r) << 16) | (Byte(g) << 8) | Byte(bl);
                if (quad.ZEnable)
                {
                    _depth[i] = z;
                }
            }
        }
    }

    private static float Edge(float ax, float ay, float bx, float by, float px, float py) => (bx - ax) * (py - ay) - (by - ay) * (px - ax);

    // A pixel exactly on an edge belongs to the triangle only for a top or left edge.
    private static bool Covers(float w, CanvasVertex p, CanvasVertex q, float area)
    {
        if (w > 0f)
        {
            return true;
        }

        if (w < 0f)
        {
            return false;
        }

        float dx = (q.X - p.X) * MathF.Sign(area), dy = (q.Y - p.Y) * MathF.Sign(area);
        return dy < 0f || (dy == 0f && dx > 0f);
    }

    private static uint Byte(float f) => (uint)Math.Clamp((int)MathF.Round(f * 255f), 0, 255);

    // Bilinear, wrapping or clamped, texel centres at half coordinates.
    private static uint Sample(RasterTexture t, float u, float v, bool clamp)
    {
        float fx = u * t.Width - 0.5f, fy = v * t.Height - 0.5f;
        int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy);
        float ax = fx - ix, ay = fy - iy;
        uint p00 = Texel(t, ix, iy, clamp), p10 = Texel(t, ix + 1, iy, clamp), p01 = Texel(t, ix, iy + 1, clamp), p11 = Texel(t, ix + 1, iy + 1, clamp);
        uint result = 0;
        for (int shift = 0; shift < 32; shift += 8)
        {
            float top = ((p00 >> shift) & 0xff) * (1f - ax) + ((p10 >> shift) & 0xff) * ax;
            float bottom = ((p01 >> shift) & 0xff) * (1f - ax) + ((p11 >> shift) & 0xff) * ax;
            result |= (uint)Math.Clamp((int)MathF.Round(top * (1f - ay) + bottom * ay), 0, 255) << shift;
        }

        return result;
    }

    private static uint Texel(RasterTexture t, int x, int y, bool clamp)
    {
        if (clamp)
        {
            return t.Pixels[Math.Clamp(y, 0, t.Height - 1) * t.Width + Math.Clamp(x, 0, t.Width - 1)];
        }

        x %= t.Width;
        y %= t.Height;
        if (x < 0) x += t.Width;
        if (y < 0) y += t.Height;
        return t.Pixels[y * t.Width + x];
    }
}
