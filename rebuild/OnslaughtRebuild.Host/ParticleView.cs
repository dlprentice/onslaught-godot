// SPDX-License-Identifier: GPL-3.0-only
// The particle sprites as retail draws them. Adapted from bea-decomp src/PDSimpleSprite.cpp (CPDSimpleSprite::RenderSprites
// 004c5d50, BuildFrameUVs 004c5c50, the colour range's colour) and DXParticleManager.cpp (RenderOther's states and the
// camera basis, 0054f7e0); the particles themselves are the parity core's.

using Godot;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Particles;

namespace OnslaughtRebuild.Host;

/// <summary>
/// One render's sprites: each live sprite descriptor's particles, oldest first, as quads in their texture's buffer, then
/// each texture's buffer drawn (newest texture first). States: no lighting, no culling, no depth writes, no fog; the
/// texture times the vertex colour, blended ONE / INVSRCALPHA (the colour is packed per blend mode: mode 0 adds).
/// The drawing does not write back to the core: the core's 16-frame visibility cache is recomputed every render here.
/// </summary>
public static class ParticleView
{
    private const string Shader = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, depth_draw_never, fog_disabled, blend_premul_alpha;
        uniform sampler2D sprite : filter_linear_mipmap, repeat_disable;
        void fragment() {
            vec4 t = texture(sprite, UV);
            ALBEDO = t.rgb * COLOR.rgb;
            ALPHA = t.a * COLOR.a;
        }
        """;

    private static Shader? _shader;

    public static Node3D Build(ParticleManager manager, TextureLibrary textures, FVector campos, FMatrix cori, float eventTime)
    {
        var buffers = new Dictionary<ParticleTexture, SurfaceTool>();
        try
        {
            // The camera's Y column is its look direction (named up in retail's drawing).
            FVector up = new(cori.Row0.Y, cori.Row1.Y, cori.Row2.Y);
            FVector right = new(cori.Row0.X, cori.Row1.X, cori.Row2.X);
            FVector back = new(-cori.Row0.Z, -cori.Row1.Z, -cori.Row2.Z);
            for (ParticleDescriptor? pd = manager.Set.FirstNode; pd is not null; pd = pd.NodeNext)
            {
                if (pd is PdSimpleSprite { Texture: { } texture } sprite)
                {
                    if (!buffers.TryGetValue(texture, out SurfaceTool? st))
                    {
                        st = new SurfaceTool();
                        st.Begin(Mesh.PrimitiveType.Triangles);
                        buffers[texture] = st;
                    }

                    Sprites(manager, sprite, st, campos, up, right, back, eventTime);
                }
            }

            var root = new Node3D { Name = "Particles" };
            _shader ??= new Shader { Code = Shader };
            int priority = 0;
            foreach (ParticleTexture texture in manager.Set.Textures)
            {
                if (!buffers.TryGetValue(texture, out SurfaceTool? st))
                {
                    continue;
                }

                using Godot.Collections.Array arrays = st.CommitToArrays();
                if (((Vector3[])arrays[(int)Mesh.ArrayType.Vertex]).Length == 0)
                {
                    continue;
                }

                int format = texture.Mode is 2 or 3 ? 2 : 5;
                ImageTexture? image = textures.Find(texture.GamePath, format);
                if (image is null)
                {
                    GD.Print($"HOST: no particle texture {texture.GamePath}");
                    continue;
                }

                var mesh = new ArrayMesh();
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                var material = new ShaderMaterial { Shader = _shader, RenderPriority = Math.Min(priority++, 127) };
                material.SetShaderParameter("sprite", image);
                mesh.SurfaceSetMaterial(0, material);
                root.AddChild(new MeshInstance3D { Mesh = mesh, Name = texture.GamePath });
            }

            return root;
        }
        finally
        {
            // These native geometry builders are not attached to the returned subtree. Free even empty/missing
            // texture builders and failed builds here; TransientHostView can only own the finished resources.
            foreach (SurfaceTool buffer in buffers.Values)
            {
                buffer.Dispose();
            }
        }
    }

    private static void Sprites(ParticleManager m, PdSimpleSprite pd, SurfaceTool st, FVector campos, FVector up, FVector right,
        FVector back, float eventTime)
    {
        float invlife = pd.Life == 0 ? 0 : 1.0f / pd.Life;
        int fadescale = X87.Fist(invlife * 65536.0f);
        for (Particle? p = pd.OldestParticle; p is not null; p = p.PDNext)
        {
            FoR? f = p.FoR;
            bool hasori = f is { HasOrientation: true };
            if (p.Life < 0 && p.Life != -2.0f)
            {
                continue;
            }

            if (f is not null && f.UnknownAC != -1.0f && eventTime - 0.09999f > f.UnknownAC)
            {
                continue;
            }

            FVector pos = p.Pos;
            if (f is not null)
            {
                pos += f.StartPos;
            }

            FVector d = campos - pos;
            if (!((p.Radius * p.Radius / d.MagnitudeSq()) > 9.7656248e-6f || pd.TwoD != 0))
            {
                continue;
            }

            float renderRadius = p.Radius;
            if (pd.TwoD != 0)
            {
                FVector dd = campos - p.Pos;
                float ddsq = dd.MagnitudeSq();
                if (ddsq > 1.0f)
                {
                    renderRadius = MathF.Sqrt(ddsq) * p.Radius;
                }
            }

            FVector renderPos = p.Pos;
            FMatrix forori = f?.Orientation ?? FMatrix.Identity;
            if (f is not null)
            {
                if (hasori)
                {
                    renderPos = forori.Transform(renderPos);
                }

                renderPos += f.StartPos;
            }

            float u0 = p.U0, v0 = p.V0, u1 = p.U1, v1 = p.V1;
            if (pd.AnimType != 0)
            {
                (u0, v0, u1, v1) = FrameUV(pd.TextureSize, X87.Fist(p.Frame - 0.5f) + pd.TextureNumber);
            }

            int r = 255, g = 255, b = 255;
            if (pd.ColourRange is PdColourRange range)
            {
                (r, g, b) = RangeColour(m, range, pd.Life == 0 ? 1.0f : (pd.Life - p.Life) * invlife, p);
            }

            if (pd.AxisAligned == 5)
            {
                FMatrix ori = hasori ? forori.Multiply(p.Orientation) : p.Orientation;
                FVector normal = -new FVector(ori.Row0.Y, ori.Row1.Y, ori.Row2.Y);
                float facing = Math.Max(Dot(normal, up), 0);
                renderRadius = facing * p.Radius;
                int s = X87.Fist(facing * 255.0f);
                r = (r * s) >> 8;
                g = (g * s) >> 8;
                b = (b * s) >> 8;
            }

            uint colour;
            if (pd.FadeCol != 0)
            {
                int alpha = pd.Life > 0 ? (X87.Fist(p.Life) * fadescale * 255) >> 16 : 255;
                if (alpha > 30)
                {
                    alpha -= 30;
                    alpha = 256 - ((51076 - (alpha * alpha)) / 226);
                }

                colour = pd.BlendMode switch
                {
                    0 => (uint)((((r * alpha) >> 8) << 16) | (((g * alpha) >> 8) << 8) | ((b * alpha) >> 8)),
                    1 => (uint)((alpha << 24) + (r << 16) + (g << 8) + b),
                    2 => (uint)(((alpha > 127 ? Math.Min((255 - alpha) * 2, 255) : Math.Min(alpha * 8, 255)) << 24)
                        + (((alpha * r) >> 8) << 16) + (((alpha * g) >> 8) << 8) + ((alpha * b) >> 8)),
                    _ => 0,
                };
            }
            else
            {
                int rr = pd.BlendMode == 1 ? r + 0xff00 : r;
                colour = (uint)((((rr << 8) + g) << 8) + b);
            }

            (FVector axisX, FVector axisY) = Axes(m, pd, p, hasori, forori, campos, up, right, back, ref renderPos, ref renderRadius);
            axisX *= renderRadius;
            axisY *= renderRadius;
            var c = new Color(((colour >> 16) & 255) / 255f, ((colour >> 8) & 255) / 255f, (colour & 255) / 255f, (colour >> 24) / 255f);
            FVector[] corners =
            [
                renderPos - axisX + axisY, renderPos + axisX + axisY, renderPos - axisX - axisY, renderPos + axisX - axisY,
            ];
            Vector2[] uvs = [new(u0, v0), new(u1, v0), new(u0, v1), new(u1, v1)];
            foreach (int i in new[] { 1, 0, 2, 1, 2, 3 })
            {
                st.SetColor(c);
                st.SetUV(uvs[i]);
                st.AddVertex(Host.ToGodot(corners[i]));
            }
        }
    }

    private static (FVector X, FVector Y) Axes(ParticleManager m, PdSimpleSprite pd, Particle p, bool hasori, FMatrix forori,
        FVector campos, FVector up, FVector right, FVector back, ref FVector renderPos, ref float renderRadius)
    {
        static FVector ColumnX(FMatrix o) => new(o.Row0.X, o.Row1.X, o.Row2.X);
        static FVector ColumnY(FMatrix o) => new(o.Row0.Y, o.Row1.Y, o.Row2.Y);
        static FVector ColumnZ(FMatrix o) => new(o.Row0.Z, o.Row1.Z, o.Row2.Z);
        FMatrix ori = hasori ? forori.Multiply(p.Orientation) : p.Orientation;
        switch (pd.AxisAligned)
        {
            case 1:
                return (-ColumnX(ori), -ColumnY(ori));
            case 2:
                return (ColumnX(ori) * -1.0f, ColumnZ(ori) * -1.0f);
            case 3:
                return (ori.Transform(new FVector(0, 0, 1.0f)), ori.Transform(new FVector(0, -1.0f, 0)));
            case 4:
            case 5:
            {
                FVector d = renderPos - campos;
                float len = d.Magnitude();
                if (len > 0 && p.Radius / len > 0.0625f)
                {
                    FVector t = up * Dot(d, up);
                    FVector x = t - d;
                    float xl = x.Magnitude();
                    if (xl > 0)
                    {
                        x *= 1.0f / xl;
                    }

                    return (x, FVector.Cross(x, up));
                }

                return (right, back);
            }

            case 6:
            {
                float length = pd.Length.GetValueByAge(m, p.Life, pd.Life, p);
                FVector x = p.Orientation.Transform(new FVector(0, 1.0f, 0)) * length;
                if (hasori)
                {
                    x = forori.Transform(x);
                }

                FVector y = FVector.Cross(x, up);
                float mag = y.Magnitude();
                if (mag > 0.001f)
                {
                    y *= 1.0f / mag;
                }

                renderPos += x * 0.5f;
                x *= 1.0f / renderRadius;
                return (x, y);
            }

            default:
                return (right, back);
        }
    }

    private static float Dot(FVector a, FVector b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    private static (float, float, float, float) FrameUV(int size, int n)
    {
        int across = 16 >> size;
        int shift = 4 - size;
        float step = 1.0f / across;
        float u = (n & (across - 1)) * step;
        float v = (n >> shift) * step;
        return (u, v, step + u, step + v);
    }

    /// <summary>The colour range's colour at f (0 born .. 1 dead), each channel 0..255.</summary>
    private static (int, int, int) RangeColour(ParticleManager m, PdColourRange cr, float f, Particle p)
    {
        static int B(float v) => (byte)X87.Fist(v * 255.0f);
        if (cr.UseTransition != 0)
        {
            if (f < cr.TransitionPoint)
            {
                float t = f / cr.TransitionPoint;
                float u = 1.0f - t;
                return (B((cr.TransitionRed.GetValue(m, t, p) * t) + (cr.StartRed.GetValue(m, t, p) * u)),
                    B((cr.TransitionGreen.GetValue(m, t, p) * t) + (cr.StartGreen.GetValue(m, t, p) * u)),
                    B((cr.TransitionBlue.GetValue(m, t, p) * t) + (cr.StartBlue.GetValue(m, t, p) * u)));
            }
            else
            {
                float t = (f - cr.TransitionPoint) / (1.0f - cr.TransitionPoint);
                float u = 1.0f - t;
                return (B((cr.TransitionRed.GetValue(m, t, p) * u) + (cr.EndRed.GetValue(m, t, p) * t)),
                    B((cr.TransitionGreen.GetValue(m, t, p) * u) + (cr.EndGreen.GetValue(m, t, p) * t)),
                    B((cr.TransitionBlue.GetValue(m, t, p) * u) + (cr.EndBlue.GetValue(m, t, p) * t)));
            }
        }

        if (cr.UseEnd != 0)
        {
            float u = 1.0f - f;
            return (B((cr.StartRed.GetValue(m, f, p) * u) + (cr.EndRed.GetValue(m, f, p) * f)),
                B((cr.StartGreen.GetValue(m, f, p) * u) + (cr.EndGreen.GetValue(m, f, p) * f)),
                B((cr.StartBlue.GetValue(m, f, p) * u) + (cr.EndBlue.GetValue(m, f, p) * f)));
        }

        return (B(cr.StartRed.GetValue(m, f, p)), B(cr.StartGreen.GetValue(m, f, p)), B(cr.StartBlue.GetValue(m, f, p)));
    }
}
