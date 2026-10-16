// SPDX-License-Identifier: GPL-3.0-or-later
// The part buffers as CDXMeshVB holds them (bea-decomp src/DXMeshVB.cpp and DXMeshVB.h: the static and skinned
// vertices, the pieces' indices and texture slots, mPrimitiveType at +0x11c).

using System.Buffers.Binary;
using Godot;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Host;

/// <summary>
/// Things' meshes as Godot geometry, from each part's vertex buffers as the PC renderer draws them: a static vertex is
/// 0x24 bytes (position at 0, diffuse at 0x18, UV at 0x1c), a skinned one 0x30 (diffuse at 0x24, UV at 0x28); the
/// pieces' 16-bit indices form a list or strip. Each piece takes its first texture layer. Parts sit at the mesh's rest
/// pose (frame 0, as CMeshPose::Build builds it with no subject); one Godot mesh per retail mesh, placed per thing.
/// Further texture layers, blend modes, texture animation, skinning and the motion controllers' poses come later.
/// </summary>
public sealed class MeshView(TextureLibrary textures, Landscape.TerrainTexture map)
{
    private readonly Dictionary<(MeshData, bool), ArrayMesh?> _built = [];

    /// <summary>
    /// CState's default render state: the vertex colour is the material's diffuse and ambient (D3DMCS_COLOR1). D3D lights
    /// it with the ambient render state (the map's ambient) and two directional lights, the sun and the anti-sun
    /// (CEngine::SetupLights; each byte / 256). The lights carry no ambient for meshes: CRenderInfo::Apply passes
    /// ApplyLight(i, FALSE) (renderinfo.cpp:244); only the landscape applies them with ambient. The texture modulates the
    /// result, doubled (USE_MODULATE_2X).
    /// </summary>
    private const string Shader = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, fog_disabled;
        // Retail blends in gamma space, and this host's output is not converted from linear (the Compatibility
        // renderer here: a constant 0.5 put through the sRGB-to-linear curve read back as 54, i.e. 0.212), so ALBEDO
        // takes the gamma-space colour as it is.
        vec3 to_linear(vec3 c) { return c; }
        // D3D's EXP table fog (FOGTABLEMODE 1, the level's colour and density) blended in gamma space as retail does,
        // on the view depth; Godot's own fog would blend in linear space.
        uniform vec3 fog_colour;
        uniform float fog_density;
        vec3 fogged(vec3 c, float depth) { return mix(fog_colour, c, clamp(exp(-fog_density * depth), 0.0, 1.0)); }
        uniform sampler2D albedo : filter_linear_mipmap, repeat_enable;
        uniform bool textured;
        uniform bool texture_only;
        uniform bool lit_only;
        uniform int debug_view;
        varying vec3 world_normal;
        uniform vec3 ambient;
        uniform vec3 sun;
        uniform vec3 anti_sun;
        uniform vec3 sun_dir;
        varying vec3 lit;
        void vertex() {
            vec3 n = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
            vec3 l = ambient + sun * max(dot(n, -sun_dir), 0.0) + anti_sun * max(dot(n, sun_dir), 0.0);
            lit = clamp(COLOR.rgb * l, 0.0, 1.0);
            world_normal = n;
        }
        void fragment() {
            vec4 t = textured ? texture(albedo, UV) : vec4(1.0);
            ALBEDO = debug_view == 3 ? to_linear(vec3(0.5))
                : debug_view == 1 ? to_linear(world_normal * 0.5 + 0.5)
                : debug_view == 2 ? to_linear(COLOR.rgb)
                : lit_only ? to_linear(lit) : texture_only ? to_linear(t.rgb)
                : to_linear(fogged(clamp(t.rgb * lit * 2.0, 0.0, 1.0), -VERTEX.z));
            ALPHA = t.a * COLOR.a;
            ALPHA_SCISSOR_THRESHOLD = 0.5;
        }
        """;

    private Shader? _shader;
    private bool _loggedSun;

    private readonly Dictionary<(MeshData, int), ArrayMesh?> _builtParts = [];

    /// <summary>
    /// The thing's mesh as retail draws it: whole in its rest pose, or, for a thing with a motion controller, part by part
    /// at the controller's transforms between its last two rebuilds by the render <paramref name="fraction"/> (each part's
    /// vertices are in its own space, and retail sets each part's world matrix).
    /// </summary>
    public Node3D? Build(Thing thing, float fraction)
    {
        MeshData? mesh = thing.RenderThing?.MeshOrNull;
        if (mesh is null || mesh.NumParts == 0)
        {
            return null;
        }

        if (thing is BattleEngine { MotionController: { } mc })
        {
            var root = new Node3D { Name = mesh.HeaderName };
            foreach (MeshPart part in mesh.Parts)
            {
                if (mc.GetPartTransform(part.PartNumber, fraction) is not { } t)
                {
                    return BuildWhole(thing, mesh);
                }

                if (!_builtParts.TryGetValue((mesh, part.PartNumber), out ArrayMesh? partMesh))
                {
                    partMesh = BuildMesh(mesh, part.PartNumber);
                    _builtParts[(mesh, part.PartNumber)] = partMesh;
                }

                if (partMesh is not null)
                {
                    root.AddChild(new MeshInstance3D { Mesh = partMesh, Name = part.Name, Transform = Place(t.Pos, t.Ori) });
                }
            }

            return root;
        }

        return BuildWhole(thing, mesh);
    }

    private MeshInstance3D? BuildWhole(Thing thing, MeshData mesh)
    {
        bool tree = thing is OnslaughtRebuild.Parity.Things.Tree;
        if (!_built.TryGetValue((mesh, tree), out ArrayMesh? built))
        {
            built = BuildMesh(mesh, -1, tree);
            _built[(mesh, tree)] = built;
        }

        if (built is null)
        {
            return null;
        }

        return new MeshInstance3D { Mesh = built, Name = mesh.HeaderName, Transform = Place(thing.Pos, thing.GetOrientation()) };
    }

    /// <summary>A retail position and orientation as a Godot transform (the frame's mirror on both sides).</summary>
    public static Transform3D Place(FVector pos, FMatrix ori)
    {
        static FVector ToRetail(Vector3 g) => new(g.X, -g.Z, -g.Y);
        Vector3 Column(Vector3 axis) => Host.ToGodot(ori.Transform(ToRetail(axis)));
        return new Transform3D(new Basis(Column(Vector3.Right), Column(Vector3.Up), Column(Vector3.Back)), Host.ToGodot(pos));
    }

    private ArrayMesh? BuildMesh(MeshData mesh, int onlyPart = -1, bool tree = false)
    {
        var pose = new MeshPose(mesh.NumParts);
        pose.Build(new FVector(0, 0, 0), FMatrix.Identity, mesh.Parts[0], null, 0);
        var surfaces = new Dictionary<string, SurfaceTool>();
        foreach (MeshPart part in mesh.Parts)
        {
            MeshVertexBuffer? vb = part.GetMeshDataPart()?.VertexBuffer;
            if (vb is null || vb.Vertices.Length == 0)
            {
                continue;
            }

            int stride = vb.Stride;
            bool skinned = stride == 0x30;
            int primitive = BinaryPrimitives.ReadInt32LittleEndian(vb.Record.AsSpan(0x11c));
            if (onlyPart >= 0 && part.PartNumber != onlyPart)
            {
                continue;
            }

            // One part alone is built in its own space; the whole mesh in its rest pose.
            FMatrix partOri = onlyPart >= 0 ? FMatrix.Identity : pose.Orientations[part.PartNumber];
            FVector partPos = onlyPart >= 0 ? new FVector(0, 0, 0) : pose.Positions[part.PartNumber];
            int count = vb.Vertices.Length / stride;
            foreach (MeshVertexBufferPiece piece in vb.Pieces)
            {
                int slot = piece.TextureIndices[0];
                string texture = slot >= 0 && slot < mesh.Textures.Count ? mesh.Textures[slot].TextureName : "";
                if (!surfaces.TryGetValue(texture, out SurfaceTool? st))
                {
                    st = new SurfaceTool();
                    st.Begin(Mesh.PrimitiveType.Triangles);
                    surfaces[texture] = st;
                }

                void Corner(int v)
                {
                    ReadOnlySpan<byte> r = vb.Vertices.AsSpan(v * stride, stride);
                    var local = new FVector(BinaryPrimitives.ReadSingleLittleEndian(r), BinaryPrimitives.ReadSingleLittleEndian(r[4..]),
                        BinaryPrimitives.ReadSingleLittleEndian(r[8..]));
                    uint diffuse = BinaryPrimitives.ReadUInt32LittleEndian(r[(skinned ? 0x24 : 0x18)..]);
                    ReadOnlySpan<byte> nr = r[(skinned ? 0x18 : 0x0c)..];
                    var normal = new FVector(BinaryPrimitives.ReadSingleLittleEndian(nr), BinaryPrimitives.ReadSingleLittleEndian(nr[4..]),
                        BinaryPrimitives.ReadSingleLittleEndian(nr[8..]));
                    st.SetNormal(Host.ToGodot(partOri.Transform(normal)));
                    int uv = skinned ? 0x28 : 0x1c;
                    st.SetColor(new Color(((diffuse >> 16) & 255) / 255f, ((diffuse >> 8) & 255) / 255f, (diffuse & 255) / 255f,
                        (diffuse >> 24) / 255f));
                    st.SetUV(new Vector2(BinaryPrimitives.ReadSingleLittleEndian(r[uv..]), BinaryPrimitives.ReadSingleLittleEndian(r[(uv + 4)..])));
                    st.AddVertex(Host.ToGodot(partOri.Transform(local) + partPos));
                }

                ushort[] ix = piece.Indices;
                for (int k = 0; k < piece.NumPrimitives; k++)
                {
                    (int a, int b, int c) = primitive == 4
                        ? (k & 1) == 0 ? (ix[k], ix[k + 1], ix[k + 2]) : (ix[k + 1], ix[k], ix[k + 2])
                        : (ix[3 * k], ix[(3 * k) + 1], ix[(3 * k) + 2]);
                    if (a >= count || b >= count || c >= count || a == b || b == c || a == c)
                    {
                        continue;
                    }

                    // The mirror into Godot's frame reverses the winding: drawn a, c, b.
                    Corner(a);
                    Corner(c);
                    Corner(b);
                }
            }
        }

        var result = new ArrayMesh();
        _shader ??= new Shader { Code = Shader };
        static Vector3 Bytes(uint c, float scale) => new Vector3((c >> 16) & 255, (c >> 8) & 255, c & 255) * scale;
        (float sx, float sy, float sz) = map.SunPos;
        Vector3 sunDir = Host.ToGodot(new FVector(-sx, -sy, -sz)).Normalized();
        if (System.Environment.GetEnvironmentVariable("HOST_FLIP_SUN") == "1")
        {
            sunDir = -sunDir; // diagnostic: the opposite light direction
        }
        if (!_loggedSun)
        {
            _loggedSun = true;
            GD.Print($"HOST: sun pos ({sx}, {sy}, {sz}), light direction (Godot) {sunDir}; ambient {map.Ambient:x8} sun {map.Sun:x8} anti {map.AntiSun:x8}");
        }
        foreach ((string texture, SurfaceTool st) in surfaces)
        {
            Godot.Collections.Array arrays = st.CommitToArrays();
            if (((Vector3[])arrays[(int)Mesh.ArrayType.Vertex]).Length == 0)
            {
                continue;
            }

            result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            ImageTexture? image = texture.Length > 0 ? textures.Get(texture) : null;
            var material = new ShaderMaterial { Shader = _shader };
            material.SetShaderParameter("textured", image is not null);
            // HOST_TEXTURE_ONLY=1 (diagnostic): the texture alone, unlit.
            material.SetShaderParameter("texture_only", System.Environment.GetEnvironmentVariable("HOST_TEXTURE_ONLY") == "1");
            // HOST_DEBUG=lit (diagnostic): the vertex lighting alone.
            material.SetShaderParameter("lit_only", System.Environment.GetEnvironmentVariable("HOST_DEBUG") == "lit");
            material.SetShaderParameter("debug_view", System.Environment.GetEnvironmentVariable("HOST_DEBUG") switch { "normal" => 1, "colour" => 2, "grey" => 3, _ => 0 });
            if (image is not null)
            {
                material.SetShaderParameter("albedo", image);
            }

            if (tree)
            {
                // CRTTree::Render's own lights (RTTree.cpp 004dd960): the sun straight down at a tenth, the anti-sun
                // straight up at full strength, and an ambient of three quarters the map's plus a quarter of the sun's
                // (green takes the sun's blue quarter, as retail does), each channel byte / 256, packed through * 255.
                static float Channel(uint c, int shift) => ((c >> shift) & 255) / 256.0f;
                uint amb = map.Ambient;
                uint sunColour = map.Sun;
                float sr = Channel(sunColour, 16) * 0.25f;
                float sb = Channel(sunColour, 0) * 0.25f;
                float red = (Channel(amb, 16) * 0.75f) + sr;
                float green = (Channel(amb, 8) * 0.75f) + sb;
                float blue = (Channel(amb, 0) * 0.75f) + sb;
                uint ambient = ((uint)(int)(red * 255.0f) << 16) | ((uint)(int)(green * 255.0f) << 8) | (uint)(int)(blue * 255.0f);
                material.SetShaderParameter("ambient", Bytes(ambient, 1 / 255f));
                material.SetShaderParameter("sun", Bytes(sunColour, 1 / 256f) * 0.1f);
                material.SetShaderParameter("anti_sun", Bytes(map.AntiSun, 1 / 256f));
                material.SetShaderParameter("sun_dir", Host.ToGodot(new FVector(0, 0, 1.0f)).Normalized());
            }
            else
            {
                material.SetShaderParameter("ambient", Bytes(map.Ambient, 1 / 255f));
                material.SetShaderParameter("sun", Bytes(map.Sun, 1 / 256f));
                material.SetShaderParameter("anti_sun", Bytes(map.AntiSun, 1 / 256f));
                material.SetShaderParameter("sun_dir", sunDir);
            }
        material.SetShaderParameter("fog_colour", Bytes(map.Fog, 1 / 255f));
        material.SetShaderParameter("fog_density", map.FogDensity);
            result.SurfaceSetMaterial(result.GetSurfaceCount() - 1, material);
        }

        return result.GetSurfaceCount() == 0 ? null : result;
    }
}
