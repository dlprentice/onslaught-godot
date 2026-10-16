// SPDX-License-Identifier: GPL-3.0-or-later
// The water as retail draws it without the GeForce FX shader path (GEFORCE_FX_POWER defaults to 0, and CSystem::Init
// then clears EXTRA_GEFORCE_FX_FEATURES, so no mirror texture exists): CDXWater::Render's water_method 2 grid. Adapted
// from bea-decomp src/DXWater.cpp (Load 0055b230, BuildGrid 0055b440, Render 0055b6c0, WATER_ADVANCE); the stage states,
// matrices and textures were checked against the port's D3D log of Present 30 (draw 223).

using Godot;
using OnslaughtRebuild.Host.Landscape;
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The water grid: 25 x 25 vertices 64 apart around the camera at the water level, each fading out with its distance
/// (alpha 500 - |xy| / 736 * 500, clamped), UVs the grid's own x and y. Three fixed-function stages: the caustic times
/// the water colour (TFACTOR), times the caustic again, plus the reflection, each through its own texture matrix; lighting
/// off, blended by the vertex alpha, then D3D's EXP fog.
/// </summary>
public static class WaterView
{
    private const float GridStart = -768.0f;
    private const float GridFade = 1.0f / 736.0f;
    private const int GridSide = 25;
    private const float GridStep = 64.0f;
    private const float Pi = 3.14159265358979f;

    private const string Shader = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, fog_disabled, blend_mix, depth_draw_always;
        uniform sampler2D caustic : filter_linear_mipmap, repeat_enable;
        uniform sampler2D reflection : filter_linear_mipmap, repeat_enable;
        uniform vec3 water_colour;
        // Each texture matrix as D3D applies it to (u, v, 1) with TTFF_COUNT2: (u, v) . [_11 _12; _21 _22] + (_31, _32).
        uniform vec4 m0; uniform vec2 o0;
        uniform vec4 m1; uniform vec2 o1;
        uniform vec4 m2; uniform vec2 o2;
        uniform vec3 fog_colour;
        uniform float fog_density;
        varying vec2 grid;
        vec2 xf(vec2 uv, vec4 m, vec2 o) { return vec2(uv.x * m.x + uv.y * m.z, uv.x * m.y + uv.y * m.w) + o; }
        void vertex() { grid = UV; }
        void fragment() {
            vec3 c = texture(caustic, xf(grid, m0, o0)).rgb * water_colour;
            c = c * texture(caustic, xf(grid, m1, o1)).rgb;
            c = clamp(c + texture(reflection, xf(grid, m2, o2)).rgb, 0.0, 1.0);
            ALBEDO = mix(fog_colour, c, clamp(exp(-fog_density * -VERTEX.z), 0.0, 1.0));
            ALPHA = COLOR.a;
        }
        """;

    private static Shader? _shader;

    /// <summary>
    /// CDXWater::Render's state for one render: the grid at (camera x, y, level), and the three matrices from the
    /// camera's position, its up column and the drift angle (WATER_ADVANCE's angle2).
    /// </summary>
    public static MeshInstance3D Build(TerrainTexture terrain, float level, TextureLibrary textures, FVector camPos,
        FMatrix camOri, float angle2)
    {
        string caustic = $@"mixers\caustic{terrain.WaterNumber:00}.tga";
        string reflection = $@"mixers\reflection{terrain.WaterNumber:00}.tga";
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        float x = GridStart;
        float y = GridStart;
        var verts = new (Vector3 Pos, Vector2 Uv, float Alpha)[GridSide * GridSide];
        int n = 0;
        for (int j = GridSide; j > 0; j--)
        {
            for (int i = GridSide; i > 0; i--)
            {
                int alpha = 500 - (int)((MathF.Sqrt((x * x) + (y * y)) * GridFade) * 500.0f);
                alpha = Math.Clamp(alpha, 0, 255);
                verts[n++] = (Host.ToGodot(new FVector(x + camPos.X, y + camPos.Y, level)), new Vector2(x, y), alpha / 255f);
                x += GridStep;
            }

            y += GridStep;
            x -= GridStep * GridSide;
        }

        void V(int i)
        {
            st.SetColor(new Color(1, 1, 1, verts[i].Alpha));
            st.SetUV(verts[i].Uv);
            st.AddVertex(verts[i].Pos);
        }

        for (int j = 0; j < GridSide - 1; j++)
        {
            for (int i = 0; i < GridSide - 1; i++)
            {
                int a = (j * GridSide) + i;
                V(a);
                V(a + GridSide);
                V(a + 1);
                V(a + 1);
                V(a + GridSide);
                V(a + GridSide + 1);
            }
        }

        // The matrices, as CDXWater::Render sets them for the grid (cx, cy the camera's position; up its Y column).
        float cx = camPos.X;
        float cy = camPos.Y;
        var up = new FVector(camOri.Row0.Y, camOri.Row1.Y, camOri.Row2.Y);
        float s = (float)Math.Sin(angle2);
        float c = (float)Math.Cos(angle2);
        float a2 = angle2 + Pi;
        float s2 = (float)Math.Sin(a2);
        float c2 = (float)Math.Cos(a2);
        var o0 = new Vector2(((s + cx) * 0.1f) + (cy * 0.03f), (c * 0.1f) - (cy * 0.1f) + (cx * 0.03f));
        var o1 = new Vector2(((s2 + cy) * 0.1f) + (cx * 0.03f), ((c2 + cx) * 0.1f) - (cy * 0.03f));
        var o2 = new Vector2((up.X * 0.02f) - (camPos.X * 0.0015f) + (cx * (1.0f / 256)),
            (up.Y * 0.02f) - (camPos.Y * 0.0015f) + (cy * (1.0f / 256)));
        GD.Print($"HOST: water {terrain.WaterNumber:00} colour {terrain.WaterColour:x8} level {level}; angle2 {angle2}; "
            + $"texture0 offset {BitConverter.SingleToUInt32Bits(o0.X):x8} {BitConverter.SingleToUInt32Bits(o0.Y):x8}");

        static Vector3 Bytes(uint col, float scale) => new Vector3((col >> 16) & 255, (col >> 8) & 255, col & 255) * scale;
        _shader ??= new Shader { Code = Shader };
        // After the surf (CDXWater::Render draws SURF first, then the grid).
        var material = new ShaderMaterial { Shader = _shader, RenderPriority = 1 };
        material.SetShaderParameter("caustic", textures.Get(caustic) ?? textures.Find(caustic, 4) ?? throw new InvalidOperationException($"no texture {caustic}"));
        material.SetShaderParameter("reflection", textures.Get(reflection) ?? textures.Find(reflection, 3) ?? throw new InvalidOperationException($"no texture {reflection}"));
        material.SetShaderParameter("water_colour", Bytes(terrain.WaterColour, 1 / 255f));
        material.SetShaderParameter("m0", new Vector4(0.1f, 0.03f, 0.03f, -0.1f));
        material.SetShaderParameter("o0", o0);
        material.SetShaderParameter("m1", new Vector4(0.03f, 0.1f, 0.1f, -0.03f));
        material.SetShaderParameter("o1", o1);
        material.SetShaderParameter("m2", new Vector4(1.0f / 256, 0, 0, 1.0f / 256));
        material.SetShaderParameter("o2", o2);
        material.SetShaderParameter("fog_colour", Bytes(terrain.Fog, 1 / 255f));
        material.SetShaderParameter("fog_density", terrain.FogDensity);
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, st.CommitToArrays());
        mesh.SurfaceSetMaterial(0, material);
        return new MeshInstance3D { Mesh = mesh, Name = "Water" };
    }

    /// <summary>WATER_ADVANCE for angle2: += the render frame length × 0.05, wrapped at 2π (viewpoint 0 only).</summary>
    public static float Advance(float angle, float frameLength)
    {
        angle += frameLength * 0.05f;
        if (angle > Pi * 2)
        {
            angle -= Pi * 2;
        }

        return angle;
    }
}
