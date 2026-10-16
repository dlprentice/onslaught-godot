// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/DXKempyCube.cpp (CDXKempyCube: the faces' vertices 006508f0, Load 005440a0, Render
// 005441b0) and src/landscape.cpp (GetCubeTextureName 0048de30). Reconstructed; not in Stuart's release.

using Godot;
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The sky cube: five textured faces (no floor) as triangle strips around the camera, drawn without fog or lighting,
/// each face's texture "cubes\cubeNN_FACE.tga" clamped at its edges.
/// </summary>
public static class SkyView
{
    private const float S = 7544.89258f;
    private const float Top = 3772.44629f;
    private const float U0 = 0.001953125f;
    private const float U1 = 0.998046875f;
    private const float V1 = 0.749023438f;

    private static readonly string[] Faces = ["cent", "up", "right", "down", "left"];

    private static readonly float[,] Verts =
    {
        { -S, -S, -S, U0, U0 }, { S, -S, -S, U1, U0 }, { -S, S, -S, U0, U1 }, { S, S, -S, U1, U1 },
        { S, S, -S, U1, U0 }, { S, S, Top, U1, V1 }, { -S, S, -S, U0, U0 }, { -S, S, Top, U0, V1 },
        { S, -S, -S, U1, U0 }, { S, -S, Top, U1, V1 }, { S, S, -S, U0, U0 }, { S, S, Top, U0, V1 },
        { -S, -S, -S, U1, U0 }, { -S, -S, Top, U1, V1 }, { S, -S, -S, U0, U0 }, { S, -S, Top, U0, V1 },
        { -S, S, -S, U1, U0 }, { -S, S, Top, U1, V1 }, { -S, -S, -S, U0, U0 }, { -S, -S, Top, U0, V1 },
    };

    private const string Shader = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, fog_disabled;
        // Retail blends in gamma space, and this host's output is not converted from linear (the Compatibility
        // renderer here: a constant 0.5 put through the sRGB-to-linear curve read back as 54, i.e. 0.212), so ALBEDO
        // takes the gamma-space colour as it is.
        vec3 to_linear(vec3 c) { return c; }
        uniform sampler2D face : filter_linear_mipmap, repeat_disable;
        void fragment() { ALBEDO = to_linear(texture(face, UV).rgb); }
        """;

    /// <summary>The cube at the camera's position (retail's world matrix for it).</summary>
    public static Node3D Build(int number, TextureLibrary textures, FVector cameraPos)
    {
        var root = new Node3D { Name = "Sky", Position = Host.ToGodot(cameraPos) };
        var shader = new Shader { Code = Shader };
        for (int f = 0; f < 5; f++)
        {
            string name = $@"cubes\cube{number:00}_{Faces[f]}.tga";
            ImageTexture texture = textures.Get(name) ?? textures.Find(name, 3) ?? throw new InvalidOperationException($"no texture {name}");
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            void V(int i)
            {
                st.SetUV(new Vector2(Verts[i, 3], Verts[i, 4]));
                st.AddVertex(Host.ToGodot(new FVector(Verts[i, 0], Verts[i, 1], Verts[i, 2])));
            }

            int b = f * 4;
            V(b);
            V(b + 1);
            V(b + 2);
            V(b + 1);
            V(b + 3);
            V(b + 2);
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("face", texture);
            root.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = material, Name = Faces[f] });
        }

        return root;
    }
}
