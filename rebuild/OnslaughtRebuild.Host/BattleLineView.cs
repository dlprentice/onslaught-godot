// SPDX-License-Identifier: GPL-3.0-or-later
// The HUD's battle line: the round map at the bottom right, coloured by the influence map. Adapted from bea-decomp
// src/DXBattleLine.cpp (UpdateHeightmap 0053a390, BuildMesh 0053a5e0, InitMipLevels 0053a930, UpdateVertexBuffer
// 0053aa40, SetupVertex 0053ab40, Render 0053abe0, RenderOverlay 0053b470, AddOverlayPoint 0053b5f0), BattleLine.cpp
// (Update 00414cb0), triangulate.cpp (CTriangulate 004f7170..004f7940) and Hud.cpp (RenderBattleline 00487d10).

using Godot;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The battle line's level data, built once (PostLoadProcess): the 128 x 128 height texture (the land inside a circle of
/// radius 46 round (48, 48), shaded by its normal; water opaque black; outside clear), the influence ramp (red, a green
/// middle texel, blue), and the mesh: CTriangulate's eight-point frame over (-4..132) with each influence node added and
/// the result relaxed.
/// </summary>
public sealed class BattleLineData
{
    private static readonly Vector2 MapMin = new(0, 0);
    private static readonly Vector2 MapSize = new(96, 96);
    private static readonly Vector2 MeshMin = new(-4, -4);
    private static readonly Vector2 MeshMax = new(132, 132);

    public required ImageTexture Height { get; init; }
    public required ImageTexture Influence { get; init; }
    public required Vector2[] Points { get; init; }
    public required int[] Indices { get; init; }
    public float ScaleX { get; init; }
    public float ScaleY { get; init; }
    public float OffsetX { get; init; }
    public float OffsetY { get; init; }

    public static BattleLineData Build(HeightField map, IReadOnlyList<InfluenceNode> nodes)
    {
        // GetExtents: the record's stored bounds (the level file carries them measured).
        byte[] r = map.Record;
        float minX = BitConverter.ToInt32(r, 0), minY = BitConverter.ToInt32(r, 4);
        float maxX = BitConverter.ToInt32(r, 8), maxY = BitConverter.ToInt32(r, 12);
        float water = BitConverter.ToSingle(r, 16);
        float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
        float hx = (maxX - minX) * 0.5f, hy = (maxY - minY) * 0.5f;
        float size = hx;
        if (size < hy)
        {
            size = hy;
        }

        size *= 1.21f;
        float loX = cx - size, hiX = size + cx, loY = cy - size, hiY = cy + size;
        float rangeX = hiX - loX, rangeY = hiY - loY;

        // UpdateHeightmap.
        var height = Image.CreateEmpty(128, 128, false, Image.Format.Rgba8);
        float scale = rangeX / MapSize.X;
        var pos = new FVector(0, loY - (MapMin.Y * scale), 0);
        for (int y = 0; y < 128; y++)
        {
            pos.X = loX - (MapMin.X * scale);
            for (int x = 0; x < 128; x++)
            {
                Color c = new(0, 0, 0, 0);
                if (((x - 48) * (x - 48)) + ((y - 48) * (y - 48)) < 46 * 46)
                {
                    c = new Color(0, 0, 0, 1);
                    if (map.Collide(pos) < water)
                    {
                        FVector n = map.Normal(pos);
                        int k = X87.Fist(((n.X + n.Y) * 4.5f) + 1.5f);
                        k = Math.Clamp(k, 0, 3);
                        c = new Color(k * 17 / 255f, k * 17 / 255f, k * 17 / 255f, 1);
                    }
                }

                height.SetPixel(x, y, c);
                pos.X += scale;
            }

            pos.Y += scale;
        }

        // InitMipLevels: the 128 x 1 ramp, A4R4G4B4 0xfc00 / 0xf090 / 0xf00c.
        var influence = Image.CreateEmpty(128, 1, false, Image.Format.Rgba8);
        for (int x = 0; x < 128; x++)
        {
            influence.SetPixel(x, 0, x < 64 ? new Color(12 * 17 / 255f, 0, 0, 1) : x > 64 ? new Color(0, 0, 12 * 17 / 255f, 1) : new Color(0, 9 * 17 / 255f, 0, 1));
        }

        // BuildMesh.
        float scaleX = MapSize.X / rangeX, scaleY = MapSize.Y / rangeY;
        float offsetX = MapMin.X - (loX * scaleX), offsetY = MapMin.Y - (loY * scaleX);
        var tri = new Triangulate(1024, MeshMin, MeshMax);
        foreach (InfluenceNode node in nodes)
        {
            tri.AddPoint(new Vector2((node.X * scaleX) + offsetX, (node.Y * scaleY) + offsetY));
        }

        tri.Relax();
        var indices = new List<int>();
        for (int i = 0; i < tri.NumTris; i++)
        {
            indices.Add(tri.Tris[i].A);
            indices.Add(tri.Tris[i].C);
            indices.Add(tri.Tris[i].B);
        }

        return new BattleLineData
        {
            Height = ImageTexture.CreateFromImage(height),
            Influence = ImageTexture.CreateFromImage(influence),
            Points = tri.Verts[..tri.NumVerts],
            Indices = [.. indices],
            ScaleX = scaleX,
            ScaleY = scaleY,
            OffsetX = offsetX,
            OffsetY = offsetY,
        };
    }

    /// <summary>RadarClamp: a map point kept within 44 of (44, 44).</summary>
    public static Vector2 RadarClamp(Vector2 p)
    {
        float dx = p.X - 44.0f;
        float dy = p.Y - 44.0f;
        float k = 44.0f / MathF.Sqrt((dy * dy) + (dx * dx));
        if (k < 1.0f)
        {
            dx *= k;
            dy *= k;
        }

        return new Vector2(dx + 44.0f, dy + 44.0f);
    }
}

/// <summary>CTriangulate in mode 1 (the eight-point frame): points split the triangle they fall in, edges flip to better shapes.</summary>
public sealed class Triangulate
{
    public struct Tri
    {
        public int A, B, C;
    }

    public Vector2[] Verts;
    public Tri[] Tris;
    public int NumVerts, NumTris;
    private readonly int _maxVerts;
    private bool _changed;

    public Triangulate(int maxPoints, Vector2 min, Vector2 max)
    {
        Verts = new Vector2[maxPoints];
        Tris = new Tri[maxPoints * 2];
        _maxVerts = maxPoints;
        Verts[0] = min;
        Verts[2] = new Vector2(max.X, min.Y);
        Verts[5] = new Vector2(min.X, max.Y);
        Verts[7] = max;
        Verts[1] = (Verts[0] + Verts[2]) * 0.5f;
        Verts[3] = (Verts[0] + Verts[5]) * 0.5f;
        Verts[4] = (Verts[2] + Verts[7]) * 0.5f;
        Verts[6] = (Verts[5] + Verts[7]) * 0.5f;
        int[,] t = { { 0, 1, 3 }, { 1, 5, 3 }, { 1, 6, 5 }, { 1, 7, 6 }, { 1, 4, 7 }, { 1, 2, 4 } };
        for (int i = 0; i < 6; i++)
        {
            Tris[i] = new Tri { A = t[i, 0], B = t[i, 1], C = t[i, 2] };
        }

        NumVerts = 8;
        NumTris = 6;
    }

    private static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    public void AddPoint(Vector2 p)
    {
        int c0 = NumTris;
        int i = 0;
        while (c0 != 0)
        {
            if (SplitTriangle(i, p))
            {
                break;
            }

            i++;
            c0--;
        }

        if (c0 == 0)
        {
            Verts[NumVerts++] = p;
        }
    }

    private bool SplitTriangle(int ti, Vector2 p)
    {
        if (NumVerts >= _maxVerts)
        {
            return false;
        }

        ref Tri t = ref Tris[ti];
        int b = t.B, c = t.C;
        Vector2 da = Verts[t.A] - p, db = Verts[b] - p, dc = Verts[c] - p;
        if (Cross(da, db) >= 0 && Cross(db, dc) >= 0 && Cross(dc, da) >= 0)
        {
            t.C = NumVerts;
            Tris[NumTris++] = new Tri { A = b, B = c, C = NumVerts };
            Tris[NumTris++] = new Tri { A = c, B = Tris[ti].A, C = NumVerts };
            Verts[NumVerts++] = p;
            int a2 = Tris[ti].A, b2 = Tris[ti].B;
            CheckEdge(a2, b2);
            CheckEdge(b2, c);
            CheckEdge(c, a2);
            return true;
        }

        return false;
    }

    private void CheckEdge(int a, int b)
    {
        int i1 = FindEdge(a, b);
        int i2 = FindEdge(b, a);
        if (i1 < 0 || i2 < 0)
        {
            return;
        }

        ref Tri t1 = ref Tris[i1];
        ref Tri t2 = ref Tris[i2];
        float edge = (Verts[t1.A] - Verts[t1.B]).Length();
        float diagonal = (Verts[t1.C] - Verts[t2.C]).Length();
        Vector2 p0 = Verts[t1.A], p1 = Verts[t1.B], p2 = Verts[t1.C], p3 = Verts[t2.C];
        if (edge / diagonal < 0.8f)
        {
            if (!(diagonal < edge))
            {
                return;
            }

            Vector2 u1 = p2 - p1, v1 = p3 - p1;
            if ((u1.X * v1.Y) - (v1.X * u1.Y) <= 0)
            {
                return;
            }

            Vector2 u2 = p3 - p0, v2 = p2 - p0;
            if ((u2.X * v2.Y) - (v2.X * u2.Y) <= 0)
            {
                return;
            }
        }
        else
        {
            Vector2 a1 = p2 - p1, b1 = p3 - p1, a2 = p3 - p0, b2 = p2 - p0;
            float area1 = (a1.X * b1.Y) - (b1.X * a1.Y);
            float area2 = (a2.X * b2.Y) - (b2.X * a2.Y);
            if (area1 <= 0 || area2 <= 0)
            {
                return;
            }

            float before = area1 > area2 ? area2 / area1 : area1 / area2;
            float area3 = (b1.X * a2.Y) - (a2.X * b1.Y);
            float area4 = (a1.X * b2.Y) - (b2.X * a1.Y);
            float after = area3 > area4 ? area4 / area3 : area3 / area4;
            if (!(before > after))
            {
                return;
            }
        }

        t1.A = t2.C;
        t2.A = t1.C;
        _changed = true;
    }

    private int FindEdge(int a, int b)
    {
        for (int i = 0; i < NumTris; i++)
        {
            ref Tri t = ref Tris[i];
            if (t.A == a && t.B == b)
            {
                return i;
            }

            if (t.B == a && t.C == b)
            {
                (t.A, t.B, t.C) = (t.B, t.C, t.A);
                return i;
            }

            if (t.C == a && t.A == b)
            {
                (t.A, t.B, t.C) = (t.C, t.A, t.B);
                return i;
            }
        }

        return -1;
    }

    public void Relax()
    {
        _changed = true;
        for (int pass = 10; pass != 0; pass--)
        {
            if (!_changed)
            {
                break;
            }

            _changed = false;
            for (int i = 0; i < NumTris; i++)
            {
                Tri t = Tris[i];
                CheckEdge(t.A, t.B);
                CheckEdge(t.B, t.C);
                CheckEdge(t.C, t.A);
            }
        }
    }
}

/// <summary>
/// RenderBattleline (single player): the circle darkener, the outline, the influence mesh (method 2: the height
/// texture's colour plus its alpha times the influence ramp at each vertex's u, added to the screen), the overlay points
/// (each objective in yellow, each Forseti squad shot at recently in red, pulsing; 8-pixel point sprites drawn dark then
/// added) and each player's battle-engine marker. The message box's pulse and overlay are not drawn yet.
/// </summary>
public static class BattleLineView
{
    private const string MeshShader = """
        shader_type canvas_item;
        render_mode blend_add, unshaded;
        uniform sampler2D height_tex : filter_linear, repeat_disable;
        uniform sampler2D influence_tex : filter_linear, repeat_disable;
        varying float v_u;
        void vertex() { v_u = COLOR.r; }
        void fragment()
        {
            vec4 h = texture(height_tex, UV);
            vec3 i = texture(influence_tex, vec2(v_u, 0.5)).rgb;
            COLOR = vec4(h.rgb + h.a * i, 1.0);
        }
        """;

    public static Node2D Build(BattleLineData data, IReadOnlyList<InfluenceNode> nodes, BattleEngine? be, IEnumerable<Thing> objectives,
        IEnumerable<Thing> squads, float time, TextureLibrary textures)
    {
        var root = new Node2D { Name = "BattleLine" };
        Texture2D? Tex(string name) => textures.Get(name) ?? textures.Find(name, 0);
        HudQuads Quads(string name, CanvasItemMaterial.BlendModeEnum blend, List<(float, float, int, float, uint, Texture2D?, float)> list)
        {
            var node = new HudQuads(list) { Name = name, Material = new CanvasItemMaterial { BlendMode = blend }, TextureFilter = CanvasItem.TextureFilterEnum.Linear };
            root.AddChild(node);
            return node;
        }

        const float Right = 640.0f, Bottom = 480.0f;

        Quads("CircleDarkener", CanvasItemMaterial.BlendModeEnum.Mix, [(Right - 121.0f, Bottom + 16.0f, 2, 0.0f, 0x7fffffffu, Tex(@"hud\v2\CircleDarkener.tga"), 1.0f)]);
        Quads("Outline", CanvasItemMaterial.BlendModeEnum.Add, [(Right - 121.0f, Bottom + 16.0f, 2, 0.0f, 0xff6f8fafu, Tex(@"hud\v2\BattleLineOutline.tga"), 1.0f)]);
        if (nodes.Count == 0)
        {
            Quads("ForsetiIcon", CanvasItemMaterial.BlendModeEnum.Mix, [(Right - 104.0f, Bottom + 16.0f - 16.0f - 32.0f, 2, 0.0f, 0xffffffffu, Tex(@"hud\ForsetiIcon.tga"), 1.0f)]);
            return root;
        }

        // UpdateVertexBuffer: the frame's eight at the middle of the ramp, then each node at its influence.
        var verts = new Vector2[data.Points.Length];
        var uvs = new Vector2[data.Points.Length];
        var cols = new Color[data.Points.Length];
        for (int i = 0; i < data.Points.Length; i++)
        {
            Vector2 p = data.Points[i];
            float u = i < 8 ? 0.50390625f : (i - 8 < nodes.Count ? (nodes[i - 8].Influence * 0.49f) + 0.50390625f : 0.50390625f);
            verts[i] = new Vector2(Right - 128.0f + (p.X - -7.0f), -14.0f - p.Y + Bottom);
            uvs[i] = new Vector2(p.X * (1.0f / 128.0f), p.Y * (1.0f / 128.0f));
            cols[i] = new Color(u, 0, 0, 1);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Color] = cols;
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        var material = new ShaderMaterial { Shader = new Shader { Code = MeshShader } };
        material.SetShaderParameter("height_tex", data.Height);
        material.SetShaderParameter("influence_tex", data.Influence);
        root.AddChild(new MeshInstance2D { Name = "Mesh", Mesh = mesh, Material = material });

        // Update and RenderOverlay: the pulse from the event time's fraction; each point dark, then added in its colour.
        int pulse = X87.Fist(((float)Math.Cos((time - MathF.Floor(time)) * 6.28318530717958f) + 1.0f) * 96.0f);
        Texture2D? marker = Tex(@"hud\marker.tga");
        var dark = new List<(float, float, int, float, uint, Texture2D?, float)>();
        var lit = new List<(float, float, int, float, uint, Texture2D?, float)>();
        void Point(FVector at, uint colour)
        {
            int r = Math.Min((pulse << 16) + (int)(colour & 0xff0000), 0xff0000);
            int g = Math.Min((int)(colour & 0xff00) + (pulse << 8), 0xff00);
            int b = Math.Min((int)(colour & 0xff) + pulse, 0xff);
            Vector2 s = BattleLineData.RadarClamp(new Vector2((at.X * data.ScaleX) + data.OffsetX, (at.Y * data.ScaleY) + data.OffsetY));
            float x = Right - 128.0f + s.X + 12.0f, y = Bottom - 18.0f - s.Y;
            dark.Add((x, y, 4, 0.0f, 0xff000000u, marker, 1.0f));
            lit.Add((x, y, 4, 0.0f, 0xff000000u | (uint)(r + g + b), marker, 1.0f));
        }

        foreach (Thing t in objectives)
        {
            Point(t.Pos, 0xffff00);
        }

        foreach (Thing t in squads)
        {
            if (t is Squad s && s.Unknown4e6610())
            {
                Point(t.Pos, 0xff0808);
            }
        }

        // Point sprites at D3DRS_POINTSIZE: min(the device's largest, 8) pixels.
        foreach ((string name, CanvasItemMaterial.BlendModeEnum blend, List<(float, float, int, float, uint, Texture2D?, float)> list) in
            new[] { ("OverlayDark", CanvasItemMaterial.BlendModeEnum.Mix, dark), ("OverlayLit", CanvasItemMaterial.BlendModeEnum.Add, lit) })
        {
            root.AddChild(new HudPoints(list, 8.0f) { Name = name, Material = new CanvasItemMaterial { BlendMode = blend }, TextureFilter = CanvasItem.TextureFilterEnum.Linear });
        }

        // The battle engine's marker, at its map point, pointing along its last orientation's forward row.
        if (be is not null)
        {
            FVector fwd = be.OldOrientation.Row1;
            float angle = -MathF.Atan2(fwd.X, fwd.Y);
            Vector2 s = BattleLineData.RadarClamp(new Vector2((be.Pos.X * data.ScaleX) + data.OffsetX, (be.Pos.Y * data.ScaleY) + data.OffsetY));
            Quads("EngineMarker", CanvasItemMaterial.BlendModeEnum.Mix,
                [(Right - 128.0f + s.X + 12.0f, Bottom - 18.0f - s.Y, 4, angle, 0xffffffffu, Tex(@"hud\V2\BattleEngineMarker.tga"), 1.0f)]);
        }

        return root;
    }
}

/// <summary>D3D point sprites: each texture drawn as a size x size square centred on its point.</summary>
public sealed partial class HudPoints(List<(float X, float Y, int Align, float Turn, uint Colour, Texture2D? Tex, float Fill)> points, float size) : Node2D
{
    public override void _Draw()
    {
        foreach ((float x, float y, _, _, uint colour, Texture2D? tex, _) in points)
        {
            if (tex is not null)
            {
                var c = new Color(((colour >> 16) & 255) / 255f, ((colour >> 8) & 255) / 255f, (colour & 255) / 255f, (colour >> 24) / 255f);
                DrawTextureRect(tex, new Rect2(x - (size / 2), y - (size / 2), size, size), false, c);
            }
        }
    }
}
