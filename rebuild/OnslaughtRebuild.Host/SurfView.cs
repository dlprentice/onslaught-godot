// SPDX-License-Identifier: GPL-3.0-or-later
// The surf along the shores, as retail draws it in the water's fixed-function path. Adapted from bea-decomp
// src/DXSurf.cpp (CDXSurf::Deserialize 00556490, SSurfStrip::Create 005565d0, Render 00556a30) and src/DXWater.cpp
// (CDXWater::Render's first matrices); the stage states were checked against the port's D3D log of Present 30 (draws 221
// and 222).

using System.Buffers.Binary;
using Godot;
using OnslaughtRebuild.Host.Landscape;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The level's SURF chunk: strips of points, three per step (the wave's foot, crest and back). Each strip draws twice
/// as triangle strips: the back (crest at alpha 0xc0 to an opaque black back) and the face (foot at alpha 0 to the crest
/// at 0xc0). Stages: the water's caustic times its colour, times the caustic again, plus the reflection, then plus the
/// waves texture times the vertex colour (MULTIPLYADD); the vertex alpha blends it; counter-clockwise faces are culled.
/// </summary>
public sealed class SurfView
{
    private static readonly uint SurfTag = ChunkReader.MakeId("SURF");
    private const float Pi = 3.14159265358979f;

    private readonly List<FVector[]> _strips = [];

    /// <summary>The resource reader's hook: CDXSurf::Deserialize's strips (each count + 1 steps of three points).</summary>
    public bool ReadChunk(uint tag, ChunkReader c)
    {
        if (tag != SurfTag || c.Size < 8)
        {
            return false;
        }

        c.GetNext();
        int strips = c.ReadInt32();
        var f = new byte[4];
        float Float()
        {
            c.Read(f);
            return BinaryPrimitives.ReadSingleLittleEndian(f);
        }

        for (int k = 0; k < strips; k++)
        {
            c.GetNext();
            int count = c.ReadInt32();
            var points = new FVector[(count + 1) * 3];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new FVector(Float(), Float(), Float());
            }

            _strips.Add(points);
        }

        return true;
    }

    public int StripCount => _strips.Count;

    private const string Shader = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, fog_disabled, blend_mix, depth_draw_always;
        uniform sampler2D caustic : filter_linear_mipmap, repeat_enable;
        uniform sampler2D reflection : filter_linear_mipmap, repeat_enable;
        uniform sampler2D waves : filter_linear_mipmap, repeat_enable;
        uniform vec3 water_colour;
        uniform vec4 m0; uniform vec2 o0;
        uniform vec4 m1; uniform vec2 o1;
        uniform vec4 m2; uniform vec2 o2;
        uniform vec4 m3; uniform vec2 o3;
        uniform vec3 fog_colour;
        uniform float fog_density;
        varying vec2 world;
        varying vec2 wave;
        vec2 xf(vec2 uv, vec4 m, vec2 o) { return vec2(uv.x * m.x + uv.y * m.z, uv.x * m.y + uv.y * m.w) + o; }
        // RENDERINFO.SetDepthBias(4): the projection's _43 less 4 * ZBIAS_SCALER (0.00009). With retail's near plane of
        // 0.1, that equals pulling a point 0.36% of its distance towards the eye, which wins the depth test against the
        // grid drawn after it at the same height.
        void vertex() {
            world = UV;
            wave = UV2;
            vec3 eye = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz * (1.0 - (4.0 * 0.00009) / 0.1);
            POSITION = PROJECTION_MATRIX * vec4(eye, 1.0);
        }
        void fragment() {
            vec3 c = texture(caustic, xf(world, m0, o0)).rgb * water_colour;
            c = c * texture(caustic, xf(world, m1, o1)).rgb;
            c = clamp(c + texture(reflection, xf(world, m2, o2)).rgb, 0.0, 1.0);
            c = clamp(c + texture(waves, xf(wave, m3, o3)).rgb * COLOR.rgb, 0.0, 1.0);
            ALBEDO = mix(fog_colour, c, clamp(exp(-fog_density * -VERTEX.z), 0.0, 1.0));
            ALPHA = COLOR.a;
        }
        """;

    /// <summary>
    /// The surf as CDXWater::Render draws it for one render: T0 and T1 carry the drift angle only, T2 the reflection's
    /// offset from the camera, T3 the waves at half scale scrolled by scroll2.
    /// </summary>
    public MeshInstance3D? Build(TerrainTexture terrain, TextureLibrary textures, FVector camPos, FMatrix camOri, float angle2,
        float scroll2)
    {
        if (_strips.Count == 0)
        {
            return null;
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (FVector[] points in _strips)
        {
            int steps = points.Length / 3;
            var face = new (FVector Pos, Vector2 Wave, uint Colour)[steps * 2];
            var back = new (FVector Pos, Vector2 Wave, uint Colour)[steps * 2];
            float t = 0;
            for (int k = 0; k < steps; k++)
            {
                float s = (float)Math.Sin(t * 0.5f) * 0.5f;
                FVector foot = points[(k * 3) + 0];
                FVector crest = points[(k * 3) + 1];
                FVector backPoint = points[(k * 3) + 2];
                face[(k * 2) + 0] = (foot, new Vector2(t, s - 0.25f - (t * 0.25f)), 0x00ffffffu);
                face[(k * 2) + 1] = (crest, new Vector2(t, s - (t * 0.25f)), 0xc0ffffffu);
                back[(k * 2) + 0] = (crest, new Vector2(t, s - (t * 0.25f)), 0xc0ffffffu);
                back[(k * 2) + 1] = (backPoint, new Vector2(t + 0.0625f, s + 2.0f - (t * 0.25f)), 0xff000000u);
                t += 0.125f;
            }

            // Render draws mVB0 (the back) and then mVB1 (the face), each a strip of steps * 2 - 2 triangles.
            AddStrip(st, back);
            AddStrip(st, face);
        }

        static Vector3 Bytes(uint col, float scale) => new Vector3((col >> 16) & 255, (col >> 8) & 255, col & 255) * scale;
        string caustic = $@"mixers\caustic{terrain.WaterNumber:00}.tga";
        string reflection = $@"mixers\reflection{terrain.WaterNumber:00}.tga";
        var material = new ShaderMaterial { Shader = new Shader { Code = Shader }, RenderPriority = 0 };
        material.SetShaderParameter("caustic", textures.Get(caustic) ?? textures.Find(caustic, 4) ?? throw new InvalidOperationException($"no texture {caustic}"));
        material.SetShaderParameter("reflection", textures.Get(reflection) ?? textures.Find(reflection, 3) ?? throw new InvalidOperationException($"no texture {reflection}"));
        material.SetShaderParameter("waves", textures.Get(@"mixers\waves.tga") ?? textures.Find(@"mixers\waves.tga", 5) ?? throw new InvalidOperationException(@"no texture mixers\waves.tga"));
        material.SetShaderParameter("water_colour", Bytes(terrain.WaterColour, 1 / 255f));
        material.SetShaderParameter("m0", new Vector4(0.1f, 0.03f, 0.03f, -0.1f));
        material.SetShaderParameter("m1", new Vector4(0.03f, 0.1f, 0.1f, -0.03f));
        material.SetShaderParameter("m2", new Vector4(1.0f / 256, 0, 0, 1.0f / 256));
        material.SetShaderParameter("m3", new Vector4(0.5f, 0, 0, 0.5f));
        UpdateOffsets(material, camPos, camOri, angle2, scroll2);
        material.SetShaderParameter("fog_colour", Bytes(terrain.Fog, 1 / 255f));
        material.SetShaderParameter("fog_density", terrain.FogDensity);
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, st.CommitToArrays());
        mesh.SurfaceSetMaterial(0, material);
        return new MeshInstance3D { Mesh = mesh, Name = "Surf" };
    }

    /// <summary>The shore geometry is fixed; a live view changes only the four texture offsets.</summary>
    public LiveView? CreateLive(TerrainTexture terrain, TextureLibrary textures, FVector camPos, FMatrix camOri,
        float angle2, float scroll2)
    {
        MeshInstance3D? node = Build(terrain, textures, camPos, camOri, angle2, scroll2);
        return node is null ? null : new LiveView(node);
    }

    public sealed class LiveView : IDisposable
    {
        private readonly ShaderMaterial _material;
        private bool _disposed;

        internal LiveView(MeshInstance3D node)
        {
            Node = node;
            _material = (ShaderMaterial)node.Mesh!.SurfaceGetMaterial(0);
        }

        public MeshInstance3D Node { get; }

        public void Update(FVector camPos, FMatrix camOri, float angle2, float scroll2)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            UpdateOffsets(_material, camPos, camOri, angle2, scroll2);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Node.GetParent()?.RemoveChild(Node);
            Node.QueueFree();
        }
    }

    private static void UpdateOffsets(ShaderMaterial material, FVector camPos, FMatrix camOri, float angle2, float scroll2)
    {
        float sn = (float)Math.Sin(angle2);
        float cs = (float)Math.Cos(angle2);
        float a = angle2 + Pi;
        var up = new FVector(camOri.Row0.Y, camOri.Row1.Y, camOri.Row2.Y);
        material.SetShaderParameter("o0", new Vector2(sn * 0.1f, cs * 0.1f));
        material.SetShaderParameter("o1", new Vector2((float)Math.Sin(a) * 0.1f, (float)Math.Cos(a) * 0.1f));
        material.SetShaderParameter("o2", new Vector2((up.X * 0.02f) - (camPos.X * 0.0015f), (up.Y * 0.02f) - (camPos.Y * 0.0015f)));
        material.SetShaderParameter("o3", new Vector2(0, scroll2));
    }

    /// <summary>A D3D triangle strip as triangles, D3D's winding (alternate triangles swapped), mirrored into Godot's frame.</summary>
    private static void AddStrip(SurfaceTool st, (FVector Pos, Vector2 Wave, uint Colour)[] v)
    {
        void V(int i)
        {
            uint col = v[i].Colour;
            st.SetColor(new Color(((col >> 16) & 255) / 255f, ((col >> 8) & 255) / 255f, (col & 255) / 255f, (col >> 24) / 255f));
            st.SetUV(new Vector2(v[i].Pos.X, v[i].Pos.Y));
            st.SetUV2(v[i].Wave);
            st.AddVertex(Host.ToGodot(v[i].Pos));
        }

        for (int k = 0; k + 2 < v.Length; k++)
        {
            (int a, int b, int c) = (k & 1) == 0 ? (k, k + 1, k + 2) : (k + 1, k, k + 2);

            // The mirror into Godot's frame reverses the winding: drawn a, c, b.
            V(a);
            V(c);
            V(b);
        }
    }
}
