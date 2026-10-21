// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;
using OnslaughtRebuild.Parity.Flow.Draw;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The front end drawn by Godot: each presented <see cref="CanvasFrame"/> as meshes in a 640 x 480 SubViewport under an
/// orthographic camera, under the game's Direct3D state (CState::UseDefault): the clear colour behind, depth tested
/// LESSEQUAL (or not at all for ALWAYS) and written, texels under alpha 8 discarded, blended source-alpha-over or added.
/// Consecutive quads with the same texture and state form one mesh, whose triangles draw in order; the meshes draw in
/// the frame's order by render priority. The viewport stores the shader's colours as they are, so blending happens on
/// the game's values as Direct3D's does. A depth function of ALWAYS still writes depth in Direct3D, but OpenGL writes none
/// with the test off, so such a quad draws twice: its colour with the test off, then its depth alone with the test
/// inverted, which writes it wherever the stored depth is nearer (where the stored depth is farther, the cleared 1,
/// later draws cannot tell the two apart).
/// </summary>
public sealed class FrontEndView
{
    private const string ShaderHead = "shader_type spatial;\nrender_mode unshaded, cull_disabled, depth_draw_always, ";
    private const string ShaderBody = """

        uniform sampler2D tex : filter_linear, repeat_enable;
        uniform sampler2D tex_clamped : filter_linear, repeat_disable;
        uniform bool clamped = false;
        uniform bool alpha_from_colour = false;
        uniform bool keep = false;

        void fragment() {
            vec4 t = clamped ? texture(tex_clamped, UV) : texture(tex, UV);
            float a = (alpha_from_colour ? 1.0 : t.a) * COLOR.a;
            if (!alpha_from_colour && a < 7.5 / 255.0) {
                discard;
            }
            ALBEDO = keep ? vec3(0.0) : t.rgb * COLOR.rgb;
            ALPHA = keep ? 0.0 : a;
        }
        """;

    private const string DepthOnlyShader = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, depth_draw_always, blend_mix, depth_test_inverted;
        uniform sampler2D tex : filter_linear, repeat_enable;

        void fragment() {
            if (texture(tex, UV).a * COLOR.a < 7.5 / 255.0) {
                discard;
            }
            ALBEDO = vec3(0.0);
            ALPHA = 0.0;
        }
        """;

    private readonly FrontEndImages _images;
    private Shader? _depthOnly;
    private readonly Dictionary<(QuadTexture, int), ShaderMaterial> _depthMaterials = [];
    private readonly Dictionary<(bool Additive, bool NoDepthTest), Shader> _shaders = [];
    private readonly Dictionary<QuadTexture, ImageTexture> _textures = [];
    private readonly Dictionary<(QuadTexture, bool, bool, bool, bool, int), ShaderMaterial> _materials = [];
    private readonly List<MeshInstance3D> _pool = [];
    private readonly Node3D _root;
    private readonly Godot.Environment _environment;

    public FrontEndView(FrontEndImages images)
    {
        _images = images;
        Viewport = new SubViewport { Size = new Vector2I(FrontEndRaster.Width, FrontEndRaster.Height), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        _root = new Node3D { Name = "FrontEnd" };
        Viewport.AddChild(_root);
        _environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Colors.Black };
        _root.AddChild(new WorldEnvironment { Environment = _environment });
        // Pixel (x, y) at depth z sits at (x + 0.5 - 320, 240 - y - 0.5, -(1 + z)): Direct3D 9's pixel centres are whole
        // coordinates, Godot's are half; the depth range is [1, 2] in front of the camera.
        _root.AddChild(new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            KeepAspect = Camera3D.KeepAspectEnum.Height,
            Size = FrontEndRaster.Height,
            Near = 0.5f,
            Far = 3f,
            Current = true,
        });
    }

    public SubViewport Viewport { get; }

    public void Show(CanvasFrame frame)
    {
        if (frame.ClearColour is { } clear)
        {
            _environment.BackgroundColor = Color.Color8((byte)(clear >> 16), (byte)(clear >> 8), (byte)clear);
        }

        int used = 0;
        IReadOnlyList<CanvasQuad> quads = frame.Quads;
        for (int start = 0; start < quads.Count;)
        {
            CanvasQuad first = quads[start];
            int end = start + 1;
            while (end < quads.Count && SameState(first, quads[end]))
            {
                end++;
            }

            MeshInstance3D instance = Instance(used);
            ArrayMesh mesh = Mesh(quads, start, end);
            instance.Mesh = mesh;
            instance.MaterialOverride = Material(first, used);
            instance.Visible = true;
            used++;
            if (first.ZEnable && first.ZAlways)
            {
                MeshInstance3D depth = Instance(used);
                depth.Mesh = mesh;
                depth.MaterialOverride = DepthMaterial(first.Texture, used);
                depth.Visible = true;
                used++;
            }

            start = end;
        }

        for (int i = used; i < _pool.Count; i++)
        {
            _pool[i].Visible = false;
        }
    }

    private static bool SameState(in CanvasQuad a, in CanvasQuad b) =>
        a.Texture == b.Texture && a.Blend == b.Blend && a.ZEnable == b.ZEnable && a.ZAlways == b.ZAlways && a.Clamp == b.Clamp;

    private MeshInstance3D Instance(int index)
    {
        while (_pool.Count <= index)
        {
            var instance = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _root.AddChild(instance);
            _pool.Add(instance);
        }

        return _pool[index];
    }

    private static ArrayMesh Mesh(IReadOnlyList<CanvasQuad> quads, int start, int end)
    {
        int n = end - start;
        var vertices = new Vector3[n * 4];
        var uvs = new Vector2[n * 4];
        var colours = new Color[n * 4];
        var indices = new int[n * 6];
        for (int q = 0; q < n; q++)
        {
            CanvasQuad quad = quads[start + q];
            ReadOnlySpan<CanvasVertex> v = [quad.V0, quad.V1, quad.V2, quad.V3];
            for (int k = 0; k < 4; k++)
            {
                vertices[q * 4 + k] = new Vector3(v[k].X + 0.5f - FrontEndRaster.Width / 2f, FrontEndRaster.Height / 2f - v[k].Y - 0.5f, -(1f + v[k].Z));
                uvs[q * 4 + k] = new Vector2(v[k].U, v[k].V);
                uint c = v[k].Colour;
                colours[q * 4 + k] = new Color(((c >> 16) & 0xff) / 255f, ((c >> 8) & 0xff) / 255f, (c & 0xff) / 255f, (c >> 24) / 255f);
            }

            int b = q * 4;
            indices[q * 6] = b;
            indices[q * 6 + 1] = b + 1;
            indices[q * 6 + 2] = b + 2;
            indices[q * 6 + 3] = b + 1;
            indices[q * 6 + 4] = b + 3;
            indices[q * 6 + 5] = b + 2;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Godot.Mesh.ArrayType.Max);
        arrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Godot.Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Godot.Mesh.ArrayType.Color] = colours;
        arrays[(int)Godot.Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private ShaderMaterial Material(in CanvasQuad quad, int order)
    {
        bool noDepthTest = !quad.ZEnable || quad.ZAlways;
        bool additive = quad.Blend == CanvasBlend.Additive;
        bool keep = quad.Blend == CanvasBlend.Keep;
        // Render priority keeps the frame's order: the transparent pass sorts by it before distance.
        int priority = Math.Min(order, 255) - 128;
        var key = (quad.Texture, additive, noDepthTest, quad.Clamp, keep, priority);
        if (!_materials.TryGetValue(key, out ShaderMaterial? material))
        {
            if (!_shaders.TryGetValue((additive, noDepthTest), out Shader? shader))
            {
                shader = new Shader
                {
                    Code = ShaderHead + (additive ? "blend_add" : "blend_mix") + (noDepthTest ? ", depth_test_disabled" : "") + ";\n" + ShaderBody,
                };
                _shaders[(additive, noDepthTest)] = shader;
            }

            material = new ShaderMaterial { Shader = shader, RenderPriority = priority };
            material.SetShaderParameter("tex", Texture(quad.Texture));
            material.SetShaderParameter("tex_clamped", Texture(quad.Texture));
            material.SetShaderParameter("clamped", quad.Clamp);
            material.SetShaderParameter("keep", keep);
            material.SetShaderParameter("alpha_from_colour", quad.Texture.Kind == CanvasTextureKind.Default);
            _materials[key] = material;
        }

        return material;
    }

    private ShaderMaterial DepthMaterial(QuadTexture texture, int order)
    {
        int priority = Math.Min(order, 255) - 128;
        if (!_depthMaterials.TryGetValue((texture, priority), out ShaderMaterial? material))
        {
            _depthOnly ??= new Shader { Code = DepthOnlyShader };
            material = new ShaderMaterial { Shader = _depthOnly, RenderPriority = priority };
            material.SetShaderParameter("tex", Texture(texture));
            _depthMaterials[(texture, priority)] = material;
        }

        return material;
    }

    private ImageTexture Texture(QuadTexture texture)
    {
        if (!_textures.TryGetValue(texture, out ImageTexture? result))
        {
            RasterTexture raster = _images.Get(texture) ?? RasterTexture.White;
            var rgba = new byte[raster.Pixels.Length * 4];
            for (int i = 0; i < raster.Pixels.Length; i++)
            {
                uint p = raster.Pixels[i];
                rgba[i * 4] = (byte)(p >> 16);
                rgba[i * 4 + 1] = (byte)(p >> 8);
                rgba[i * 4 + 2] = (byte)p;
                rgba[i * 4 + 3] = (byte)(p >> 24);
            }

            result = ImageTexture.CreateFromImage(Image.CreateFromData(raster.Width, raster.Height, false, Image.Format.Rgba8, rgba));
            _textures[texture] = result;
        }

        return result;
    }
}
