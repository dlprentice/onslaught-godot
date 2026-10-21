// SPDX-License-Identifier: GPL-3.0-or-later
// The HUD's target box, as retail draws it. Adapted from bea-decomp src/Hud.cpp (CHud::RenderTargetIndicator 00482590,
// its FHudMatrix and MakeRotF) and renderinfo.cpp (SetProjection 00550b10, SetView 00550be0). The target and its values
// are the parity core's HUD render's (WorldRender.HudTarget).

using Godot;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Render;

namespace OnslaughtRebuild.Host;

/// <summary>
/// RenderTargetIndicator's box at (42, 42): the white texture tinted by the target's side (written to the depth at 1, so
/// nothing behind it shows through the mesh), the target's mesh turning in the box's own viewport (the white texture's
/// size, a 90-degree view from 0.2 to 100, looking down at it from 1.5 radii back and 0.4 up, lit by an ambient of 0x60
/// and one light of 40 along (1, 1, -1), turned by a tenth of the render time) and the health bar beneath (a black bar,
/// then the side's colour filled from the left). The Thunderhead's imposter frame is not drawn (its imposters are not
/// ported). The box's 3D view lasts the level; the 2D parts are rebuilt each render.
/// </summary>
public sealed class TargetBoxView : IDisposable
{
    private readonly MeshView _meshes;
    private readonly SubViewport _viewport;
    private readonly Camera3D _camera;
    private readonly MeshInstance3D _instance;
    private readonly Dictionary<MeshData, ShaderMaterial[]> _materials = [];
    private bool _disposed;

    public TargetBoxView(MeshView meshes, Node parent, Vector2I size)
    {
        _meshes = meshes;
        _viewport = new SubViewport
        {
            Name = "TargetBox",
            Size = size,
            TransparentBg = true,
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        parent.AddChild(_viewport);
        var root = new Node3D();
        _viewport.AddChild(root);
        root.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0, 0, 0, 0) },
        });
        _camera = new Camera3D { Near = 0.2f, Far = 100.0f, Fov = 90.0f, Current = true };
        root.AddChild(_camera);
        _instance = new MeshInstance3D { Visible = false };
        root.AddChild(_instance);
    }

    public Vector2I Size => _viewport.Size;

    /// <summary>Shows the indicator's target (or nothing), and returns the 2D parts drawn at the box.</summary>
    public Node2D Update(HudIndicator? indicator, float renderTime, TextureLibrary textures)
    {
        var node = new Node2D { Name = "TargetIndicator" };
        if (indicator is null || indicator.Mesh.BoundingBox is not { } box || _meshes.Whole(indicator.Mesh) is not { } built)
        {
            _instance.Visible = false;
            return node;
        }

        // SetView(pitch * transposed MakeRot(0, 0.5, 0), (0, -1.5 r, -0.4 r)): the view's rows are its right, up and forward.
        float r = MathF.Max(box.Radius, 0.5f);
        var eye = new FVector(0, r * -1.5f, r * -0.4f);
        FMatrix look = FMatrix.MakeRotationPitchF(1.570796f).Multiply(HudRot(0.0f, 0.5f, 0.0f).Transpose());
        _camera.Position = Host.ToGodot(eye);
        _camera.LookAt(Host.ToGodot(eye + look.Row2), Host.ToGodot(look.Row1));

        // CMeshRenderer::Render at minus the bounding box's origin, turned (MakeRotF's yaw) by a tenth of the render time.
        FVector origin = box.Origin;
        _instance.Mesh = built;
        _instance.Transform = MeshView.Place(new FVector(-origin.X, -origin.Y, -origin.Z), HudRot(renderTime / 10.0f, 0.0f, 0.0f));
        ShaderMaterial[] materials = Materials(indicator.Mesh, built);
        for (int i = 0; i < materials.Length; i++)
        {
            _instance.SetSurfaceOverrideMaterial(i, materials[i]);
        }

        _instance.Visible = true;

        const float X = 42.0f, Y = 42.0f;
        Texture2D? white = textures.Get(@"hud\v2\white.tga") ?? textures.Find(@"hud\v2\white.tga", 0);
        uint back = indicator.Side switch { 0 => 0x3fff0000u, 1 => 0x3f0000ffu, 3 => 0x3f00ff00u, _ => 0x3f444444u };
        node.AddChild(new HudScaledSprites([(new Vector2(X, Y), 0.0f, back, white, 1.0f, 1.0f)])
        {
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mix },
        });
        node.AddChild(new TextureRect
        {
            Texture = _viewport.GetTexture(),
            Position = new Vector2(X - (Size.X / 2), Y - (Size.Y / 2)),
            Size = Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha },
        });
        if (indicator.Health is { } health && white is not null)
        {
            // The bar: 0.9 by 0.07 of the white texture, centred 28 below the box's centre; the fill (SPT_FILL_RIGHT) is
            // the left part of it.
            uint col = indicator.Side switch { 0 => 0xffcf5555u, 1 => 0xff5555cfu, 3 => 0xff55cf55u, _ => 0xff777777u };
            float w = white.GetSize().X * 0.9f, h = white.GetSize().Y * 0.07f;
            node.AddChild(new HudBars([(new Rect2(X - (w / 2), Y + 28.0f - (h / 2), w, h), 0xff000000u), (new Rect2(X - (w / 2), Y + 28.0f - (h / 2), w * health, h), col)]));
        }

        return node;
    }

    /// <summary>FHudMatrix::MakeRot / MakeRotF: yaw, pitch and roll as Hud.cpp builds them.</summary>
    private static FMatrix HudRot(float yaw, float pitch, float roll)
    {
        float cy = MathF.Cos(yaw), sy = MathF.Sin(yaw), cr = MathF.Cos(roll), sr = MathF.Sin(roll), cp = MathF.Cos(pitch), sp = MathF.Sin(pitch);
        return new FMatrix(
            new FVector((cr * cy) - (sp * sr * sy), -cp * sy, (sp * cr * sy) + (sr * cy)),
            new FVector((sp * sr * cy) + (cr * sy), cp * cy, (sr * sy) - (sp * cr * cy)),
            new FVector(-cp * sr, sp, cp * cr));
    }

    /// <summary>The mesh's materials with the box's lights in place of the level's, and no fog.</summary>
    private ShaderMaterial[] Materials(MeshData mesh, ArrayMesh built)
    {
        if (!_materials.TryGetValue(mesh, out ShaderMaterial[]? materials))
        {
            materials = new ShaderMaterial[built.GetSurfaceCount()];
            Vector3 light = Host.ToGodot(new FVector(1.0f, 1.0f, -1.0f)).Normalized();
            for (int i = 0; i < materials.Length; i++)
            {
                var material = (ShaderMaterial)built.SurfaceGetMaterial(i).Duplicate();
                material.SetShaderParameter("ambient", new Vector3(0x60, 0x60, 0x60) / 255.0f);
                material.SetShaderParameter("sun", new Vector3(40, 40, 40) / 255.0f);
                material.SetShaderParameter("anti_sun", Vector3.Zero);
                material.SetShaderParameter("sun_dir", light);
                material.SetShaderParameter("fog_density", 0.0f);
                materials[i] = material;
            }

            _materials[mesh] = materials;
        }

        return materials;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (GodotObject.IsInstanceValid(_viewport))
        {
            _viewport.GetParent()?.RemoveChild(_viewport);
            _viewport.Free();
        }

        foreach (ShaderMaterial[] materials in _materials.Values)
        {
            foreach (ShaderMaterial material in materials)
            {
                material.Dispose();
            }
        }

        _materials.Clear();
    }
}

/// <summary>Solid rectangles in order (the white texture's sprites at a scale), each in its colour, alpha-blended.</summary>
public sealed partial class HudBars(List<(Rect2 Rect, uint Colour)> bars) : Node2D
{
    public override void _Draw()
    {
        foreach ((Rect2 rect, uint colour) in bars)
        {
            DrawRect(rect, new Color(((colour >> 16) & 255) / 255f, ((colour >> 8) & 255) / 255f, (colour & 255) / 255f, (colour >> 24) / 255f));
        }
    }
}
