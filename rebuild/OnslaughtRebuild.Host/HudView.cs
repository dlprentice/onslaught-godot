// SPDX-License-Identifier: GPL-3.0-only
// The HUD's compass, as retail draws it. Adapted from bea-decomp src/DXCompass.cpp (BuildRing 0053c1d0, the inner ring's
// texture Unknown53c510 0053c510, Render 0053cd30) and Compass.cpp (CCompass::Render 00427210: the bar lines and
// objective markers). The values are the parity core's battle engine.

using Godot;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Render;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The compass ring around the screen's centre: the inner ring's life bar (60..150 degrees clockwise from the top, green,
/// its lost part grey) and energy bar (225..360, blue, its spent part grey), each fading down its rows; the four bar-line
/// sprites at the bars' ends; the objectives' markers round the ring. Not drawn yet: the outer ring (the threat dial,
/// which needs the radar warning receiver and the "dial" byte sprite) and the threat and damage flashes.
/// </summary>
public sealed partial class HudView : Node2D
{
    private const int InnerWidth = 256;
    private const int InnerHeight = 8;
    private const float PiM2 = 6.28318530717958f;

    private ImageTexture? _inner;
    private ImageTexture? _outer;
    private bool _showOuter = true;
    private float _outerYaw;
    private Texture2D? _barLine;
    private Texture2D? _objective;
    private readonly List<(float A, float Radius, float Turn, uint Colour, Texture2D? Tex)> _sprites = [];

    /// <summary>
    /// Live-host texture ownership. The dial is immutable for a level; its inner ring's existing pixels are updated
    /// once per Present. Dispose after freeing the HUD nodes which borrow these textures. Snapshot builds omit it.
    /// </summary>
    public sealed class TextureCache : IDisposable
    {
        private ImageTexture? _innerTexture;
        private ImageTexture? _outerTexture;
        private bool _disposed;

        internal ImageTexture Outer(byte[]? dial)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _outerTexture ??= OuterRing(dial);
        }

        internal ImageTexture Inner(Image image)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_innerTexture is null)
            {
                _innerTexture = ImageTexture.CreateFromImage(image);
            }
            else
            {
                _innerTexture.Update(image);
            }

            return _innerTexture;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _innerTexture?.Dispose();
            _outerTexture?.Dispose();
            _innerTexture = null;
            _outerTexture = null;
        }
    }

    /// <summary>The compass for one render, from the battle engine's state.</summary>
    public static HudView Build(BattleEngine be, TextureLibrary textures, IEnumerable<Thing> objectives, ref int lastHealth, ref int lastEnergy,
        byte[]? dial, float fraction, IEnumerable<Thing>? units = null, AmmoEase? ammo = null, float renderFrameLength = 1.0f,
        TextureCache? textureCache = null, bool crosshair = true)
    {
        var view = new HudView { Name = "Hud" };
        view._outer = textureCache is null ? OuterRing(dial) : textureCache.Outer(dial);
        // GetInterpolatedEulerOrientation's yaw: the last update's towards this one's.
        view._outerYaw = be.OldEulerAngles.Yaw + ((be.CurrentOrientation.Yaw - be.OldEulerAngles.Yaw) * fraction);
        float initialLife = be.Configuration?.Life ?? 1.0f;
        float maxEnergy = be.Configuration?.Energy ?? 1.0f;
        int health = (InnerWidth * 150 / 360) - X87.Fist(be.Life * ((InnerWidth * 150 / 360) - (InnerWidth * 60 / 360)) / initialLife);
        int energy = (InnerWidth * 225 / 360) + X87.Fist(be.Energy * ((InnerWidth * 360 / 360) - (InnerWidth * 225 / 360)) / maxEnergy);
        (int r, int g, int b) h = (1, 8, 1), h0 = (3, 3, 3), e0 = (3, 3, 3), e = (1, 1, 8);
        if (lastHealth == int.MinValue)
        {
            // A host capture has no previous render: start from the steady state (the ends unchanged).
            lastHealth = health;
            lastEnergy = energy;
        }

        if (health < lastHealth)
        {
            h = (15, 1, 1);
        }

        if (energy > lastEnergy)
        {
            e = (1, 5, 8);
        }

        lastHealth = health;
        lastEnergy = energy;

        // The HUD's parts (HUD.IsComponentVisible): a hidden health or energy part leaves its half of the inner ring clear
        // and its bar lines out (DXCompass 0053c510, CCompass::Render 00427210), a hidden compass its outer ring, a hidden
        // radar its outline, and a hidden current weapon part its charge and heat bars (RenderWeaponPanel, Render00486940).
        HudParts parts = be.Context.HudParts;
        bool showHealth = parts.IsComponentVisible(HudParts.HealthBar), showEnergy = parts.IsComponentVisible(HudParts.EnergyBar);
        view._showOuter = parts.IsComponentVisible(HudParts.Compass);
        bool showRadar = parts.IsComponentVisible(HudParts.Radar), showWeapon = parts.IsComponentVisible(HudParts.CurrentWeapon);

        // A4R4G4B4 rows: row 0 and the last clear; row 1 (the outer edge) brightest, fading towards the inner edge.
        using var image = Image.CreateEmpty(InnerWidth, InnerHeight, false, Image.Format.Rgba8);
        int row = 1;
        for (int n = InnerHeight - 2; n > 0; n--, row++)
        {
            int d = InnerHeight - 2;
            int alpha = 4 * n / d;
            Color C((int r, int g, int b) c) => new Color(c.r * n / d * 17 / 255f, c.g * n / d * 17 / 255f, c.b * n / d * 17 / 255f, alpha * 17 / 255f);
            for (int x = 0; x < InnerWidth; x++)
            {
                Color col = x < InnerWidth * 60 / 360 ? new Color(0, 0, 0, 0)
                    : x < InnerWidth * 150 / 360 ? (!showHealth ? new Color(0, 0, 0, 0) : x < health ? C(h0) : C(h))
                    : x < InnerWidth * 225 / 360 ? new Color(0, 0, 0, 0)
                    : !showEnergy ? new Color(0, 0, 0, 0)
                    : x < energy ? C(e)
                    : C(e0);
                image.SetPixel(x, row, col);
            }
        }

        view._inner = textureCache is null ? ImageTexture.CreateFromImage(image) : textureCache.Inner(image);
        view._barLine = textures.Get(@"hud\v2\BarLine.tga") ?? textures.Find(@"hud\v2\BarLine.tga", 0);
        view._objective = textures.Get(@"hud\v2\CompassObjectiveMarker.tga") ?? textures.Find(@"hud\v2\CompassObjectiveMarker.tga", 0);

        // The bar lines: each bar's moving end and its fixed start, at radius 110.
        int[] ends = [health, energy, InnerWidth * 150 / 360, InnerWidth * 225 / 360];
        for (int bar = 0; bar < 4; bar++)
        {
            if (!(bar % 2 == 0 ? showHealth : showEnergy))
            {
                continue;
            }

            int pos = ends[bar];
            float a = (float)pos / InnerWidth * PiM2;
            view._sprites.Add((a, 110.0f, a, 0xff1f1f1fu, view._barLine));
        }

        float yaw = be.CurrentOrientation.Yaw;
        foreach (Thing t in objectives)
        {
            FVector d = t.Pos - be.Pos;
            float a = yaw - -MathF.Atan2(d.X, d.Y);
            while (a < 0)
            {
                a += PiM2;
            }

            while (a > PiM2)
            {
                a -= PiM2;
            }

            view._sprites.Add((a, 98.0f, 0.0f, 0xffffef1fu, view._objective));
        }

        view.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha };
        view.TextureFilter = TextureFilterEnum.Linear;

        // The bar lines are drawn additively (HUD.SetAdditive), the objective markers alpha-blended (SetAlphaBlend).
        var bars = new HudSprites(view._sprites.Where(sp => sp.Tex == view._barLine).ToList())
        {
            Name = "Bars",
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            TextureFilter = TextureFilterEnum.Linear,
        };
        var marks = new HudSprites(view._sprites.Where(sp => sp.Tex != view._barLine).ToList())
        {
            Name = "Objectives",
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mix },
            TextureFilter = TextureFilterEnum.Linear,
        };
        view.AddChild(bars);
        view.AddChild(marks);

        // RenderTargetMarkers' crosshair at rest, for a single picture: the screen's centre, nothing over it, white (primary
        // 0xaf), the secondary and dot at half alpha (0x7f), scale 1. The live host draws it eased (HudTargetsView).
        Texture2D? Tex(string name) => textures.Get(name) ?? textures.Find(name, 0);
        if (crosshair)
        {
            view.AddChild(new HudSprites(
            [
                (0.0f, 0.0f, 0.0f, 0xafffffffu, Tex(@"hud\v3\hud_crosshair_primary.tga")),
                (0.0f, 0.0f, 0.0f, 0x7fffffffu, Tex(@"hud\v3\hud_crosshair_secondary.tga")),
                (0.0f, 0.0f, 0.0f, 0x7fffffffu, Tex(@"hud\v3\hud_crosshair_dot.tga")),
            ])
            {
                Name = "Crosshair",
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mix },
                TextureFilter = TextureFilterEnum.Linear,
            });
        }

        // RenderWeaponPanel: the radio's backing, the radar outline, the weapon charge and the north pointer.
        Node2D Run(string name, CanvasItemMaterial.BlendModeEnum blend, List<(float, float, int, float, uint, Texture2D?, float)> list)
        {
            var node = new HudQuads(list)
            {
                Name = name,
                Material = new CanvasItemMaterial { BlendMode = blend },
                TextureFilter = TextureFilterEnum.Linear,
            };
            view.AddChild(node);
            return node;
        }

        const float Bottom = 480.0f;
        Run("RadioView", CanvasItemMaterial.BlendModeEnum.Mix, [(17.0f, Bottom + 16.0f, 2, 0.0f, 0x6fffffffu, Tex(@"hud\v2\RadioView.tga"), 1.0f)]);
        if (showRadar)
        {
            Run("RadarOutline", CanvasItemMaterial.BlendModeEnum.Add, [(17.0f, Bottom + 16.0f, 2, 0.0f, 0xff6f8fafu, Tex(@"hud\v2\RadarOutline.tga"), 1.0f)]);
        }

        float charge = 0.0f;
        if (be.WalkerPart?.GetCurrentWeapon() is { } weapon && weapon.GetMaxCharge() != 0)
        {
            charge = weapon.ChargeValue / weapon.GetMaxCharge();
        }

        Texture2D? fillTex = Tex(@"hud\v2\WeaponFill.tga");
        if (showWeapon)
        {
            Run("WeaponFill", CanvasItemMaterial.BlendModeEnum.Mix,
            [
                (9.0f, Bottom - 13.0f, 2, 0.0f, 0x3f000000u, fillTex, 1.0f),
                (9.0f, Bottom - 13.0f, 2, 0.0f, 0x7f7fff3fu, fillTex, MathF.Min(charge, 1.0f)),
            ]);
            Run("WeaponOutline", CanvasItemMaterial.BlendModeEnum.Add, [(9.0f, Bottom - 13.0f, 2, 0.0f, 0xffaf8f6fu, Tex(@"hud\v2\WeaponOutline.tga"), 1.0f)]);
        }

        float iy = view._outerYaw;
        float dx = -MathF.Sin(iy) * -45.0f, dy = MathF.Cos(iy) * -45.0f;
        Run("RadioNorth", CanvasItemMaterial.BlendModeEnum.Mix,
            [(17.0f + 48.0f + dx, Bottom + 16.0f - 128.0f + 48.0f + dy, 4, iy, 0xff5f7fffu, Tex(@"hud\v2\RadioNorth.tga"), 1.0f)]);

        // Render00486940: an energy weapon's heat bar on the right (filled from the right, its alpha easing to 0.8, or
        // flashing between 0.8 and 0.1 while overheated), and its alpha eased only while the weapon part shows; a store
        // weapon's count is text, which the core's HUD render writes.
        if (showWeapon && be.WalkerPart?.GetCurrentWeapon() is { } current && ammo is not null)
        {
            int store = current.Data.AmmoStore;
            if (be.StoreHeat[store])
            {
                float value = be.StoreValue[store] / be.Configuration!.StoreValue[store];
                float amount = value > 1.0f ? 1.0f : value;
                float empty = 1.0f - amount;
                float a63 = amount * 63.0f;
                uint col = (uint)((((X87.Fist((empty * 127.0f) + (amount * 255.0f)) << 8) + X87.Fist((empty * 255.0f) + a63)) << 8)
                    + X87.Fist((empty * 63.0f) + a63));
                if (be.StoreOverheat[store])
                {
                    if (ammo.Alpha == ammo.Target)
                    {
                        ammo.Target = ammo.Target == 0.8f ? 0.1f : 0.8f;
                    }
                }
                else
                {
                    ammo.Target = 0.8f;
                }

                float step = renderFrameLength * 0.1f;
                if (ammo.Alpha + step < ammo.Target)
                {
                    ammo.Alpha += step;
                }
                else if (ammo.Alpha - step > ammo.Target)
                {
                    ammo.Alpha -= step;
                }
                else
                {
                    ammo.Alpha = ammo.Target;
                }

                col += (uint)X87.Fist(ammo.Alpha * 255.0f) << 24;
                const float X = 640.0f - 122.0f + 9.0f - 28.0f;
                Run("AmmoFill", CanvasItemMaterial.BlendModeEnum.Mix,
                [
                    (X, Bottom - 13.0f, 2, 0.0f, 0x3f000000u, fillTex, -1.0f),
                    (X, Bottom - 13.0f, 2, 0.0f, col, fillTex, -amount),
                ]);
                Run("AmmoOutline", CanvasItemMaterial.BlendModeEnum.Add, [(X, Bottom - 13.0f, 2, 0.0f, 0xffaf8f6fu, Tex(@"hud\v2\WeaponOutline.tga"), -1.0f)]);
            }
        }

        // RenderRadar(96): the units round the engine, turned with it, by side; pads, objectives and engines on top.
        if (units is not null)
        {
            Run("Radar", CanvasItemMaterial.BlendModeEnum.Mix, RadarBlobs(be, units, objectives, Tex, -iy));
        }

        return view;
    }

    private static List<(float, float, int, float, uint, Texture2D?, float)> RadarBlobs(BattleEngine be, IEnumerable<Thing> units,
        IEnumerable<Thing> objectives, Func<string, Texture2D?> tex, float yaw)
    {
        const float Size = 96.0f;
        const float Cy = 480.0f - 44.0f;
        var blobs = new List<(float, float, int, float, uint, Texture2D?, float)>();
        FVector centre = ((IRenderSubject)be).RenderPos;
        float s = MathF.Sin(yaw) * 40.0f / Size;
        float c = MathF.Cos(yaw) * 40.0f / Size;
        Texture2D? small = tex(@"hud\ScannerBlobSmall.tga"), medium = tex(@"hud\ScannerBlobMedium.tga");
        Texture2D? large = tex(@"hud\ScannerBlobLarge.tga"), pad = tex(@"hud\ScannerBlobRepairPad.tga");

        Texture2D? BlobFor(Thing thing)
        {
            if (thing.IsA(ThingType.Unknown08000000))
            {
                return large;
            }

            if (thing.IsA(ThingType.Unit) && thing is Unit { UnitData: { } data })
            {
                if (data.Big != 0 || data.Behaviour is UnitBehaviour.Bomber or UnitBehaviour.ThunderHead or UnitBehaviour.Warspite
                    or UnitBehaviour.WarspiteDome)
                {
                    return large;
                }
            }

            if (thing.IsA(ThingType.Infantry))
            {
                return small;
            }

            return thing.IsA(ThingType.Building | ThingType.Naval | ThingType.Unknown04000000) || thing.IsA(ThingType.Animal) ? large : medium;
        }

        (float X, float Y, float D) Turned(Thing thing)
        {
            FVector pos = thing is IRenderSubject subject ? subject.RenderPos : thing.Pos;
            float dx = pos.X - centre.X, dy = pos.Y - centre.Y;
            float tx = (dx * c) - (dy * s);
            float ty = (dy * c) + (dx * s);
            return (tx, ty, (ty * ty) + (tx * tx));
        }

        void Add(float tx, float ty, Texture2D? t, uint col) => blobs.Add((69.0f + tx - 1.0f, Cy - 20.0f - ty + 1.0f, 4, 0.0f, col, t, 1.0f));

        void Faded(Thing thing, uint col)
        {
            (float tx, float ty, float d2) = Turned(thing);
            if (!(d2 < 8464.0f))
            {
                return;
            }

            float d = MathF.Sqrt(d2);
            uint alpha = 0xff000000;
            float k = 46.0f / d;
            if (k < 1.0f)
            {
                tx *= k;
                ty *= k;
                alpha = unchecked((uint)(X87.Fist((1.0f - ((d - 46.0f) * (1.0f / 46.0f))) * 255.0f) << 24));
            }

            Add(tx, ty, BlobFor(thing), alpha + col);
        }

        void Clamped(Thing thing, Texture2D? t, uint col)
        {
            (float tx, float ty, float d2) = Turned(thing);
            float k = 46.0f / MathF.Sqrt(d2);
            if (k < 1.0f)
            {
                tx *= k;
                ty *= k;
            }

            Add(tx, ty, t, col);
        }

        var engines = new List<Unit>();
        var pads = new List<Unit>();
        var side1 = new List<Unit>();
        var side0 = new List<Unit>();
        foreach (Thing thing in units)
        {
            if (thing is not Unit unit || unit.IsDying)
            {
                continue;
            }

            if (unit.IsA(ThingType.BattleEngine))
            {
                engines.Add(unit);
            }
            else if (unit.AI is RepairPadAI)
            {
                pads.Add(unit);
            }
            else if (unit.Allegiance == 1)
            {
                side1.Add(unit);
            }
            else if (unit.Allegiance == 0)
            {
                side0.Add(unit);
            }
            else if (!unit.IsObjective() && (unit.Flags & ThingFlags.Invisible) == 0 && unit.RenderThing is not null)
            {
                Faded(unit, unit.IsA(ThingType.AirUnit) ? 0x707070u : 0x606060u);
            }
        }

        foreach (Unit unit in side0)
        {
            Faded(unit, unit.IsA(ThingType.AirUnit) ? 0x9090ffu : 0x5050afu);
        }

        foreach (Unit unit in side1)
        {
            Faded(unit, unit.IsA(ThingType.AirUnit) ? 0xff5050u : 0xaf0808u);
        }

        foreach (Unit unit in pads)
        {
            uint col = unit.Allegiance switch { 0 => 0x5050afu, 1 => 0xaf0808u, 2 => 0x606060u, _ => 0u };
            Clamped(unit, pad, col + 0xff000000u);
        }

        foreach (Thing thing in objectives)
        {
            Clamped(thing, BlobFor(thing), 0xffffff00u);
        }

        foreach (Unit unit in engines)
        {
            Clamped(unit, medium, 0xffffffffu);
        }

        return blobs;
    }

    /// <summary>
    /// DrawThreats (0053c2e0) with no threats (the radar warning receiver is not ported): a 512 x 30 byte map, clear, its
    /// rows 13..17 set to 2 (a faint band), the "dial" byte sprite's frame 0 centred at (0, 15) wrapping round; then the
    /// 512 x 32 texture's rows 1..30 through the palette 0, 0x2222, 0x2444, 0x2666 (A4R4G4B4).
    /// </summary>
    private static ImageTexture OuterRing(byte[]? dial)
    {
        var map = new byte[512 * 30];
        for (int i = 0x1a00; i < 0x1a00 + 0xa00; i++)
        {
            map[i] = 2;
        }

        if (dial is not null && dial.Length >= 256)
        {
            // Frame 0: 16 x 16 indices, those under 4 opaque, centred on the point (the encoder's offsets less half the size).
            for (int fy = 0; fy < 16; fy++)
            {
                for (int fx = 0; fx < 16; fx++)
                {
                    byte v = dial[(fy * 16) + fx];
                    int y = 15 + fy - 8;
                    if (v < 4 && y >= 0 && y < 30)
                    {
                        map[(y * 512) + (((fx - 8) % 512) + 512) % 512] = v;
                    }
                }
            }
        }

        int[] palette = [0, 0x2222, 0x2444, 0x2666];
        using var image = Image.CreateEmpty(512, 32, false, Image.Format.Rgba8);
        for (int y = 0; y < 30; y++)
        {
            for (int x = 0; x < 512; x++)
            {
                int p = palette[map[(y * 512) + x] & 3];
                image.SetPixel(x, y + 1, new Color(((p >> 8) & 15) * 17 / 255f, ((p >> 4) & 15) * 17 / 255f, (p & 15) * 17 / 255f, ((p >> 12) & 15) * 17 / 255f));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private void Ring(Texture2D? tex, int segments, float radius, float thickness, float turn)
    {
        float outer = (thickness + 1.0f) * radius * 320.0f;
        float inner = (1.0f - thickness) * radius * 320.0f;
        var centre = new Vector2(320, 240);
        float c = MathF.Cos(turn), s = MathF.Sin(turn);
        Vector2 P(float a, float rad)
        {
            // The vertex (sin a, cos a) * rad through the world matrix's turn (row vector times [c -s; s c]), y up.
            float x = MathF.Sin(a) * rad, y = MathF.Cos(a) * rad;
            return centre + new Vector2((x * c) + (y * s), -((x * -s) + (y * c)));
        }

        for (int k = 0; k < segments; k++)
        {
            float a0 = k * PiM2 / segments;
            float a1 = (k + 1) * PiM2 / segments;
            float u0 = (float)k / segments;
            float u1 = (float)(k + 1) / segments;
            Vector2[] pts = [P(a0, inner), P(a0, outer), P(a1, outer), P(a1, inner)];
            Vector2[] uvs = [new(u0, 1), new(u0, 0), new(u1, 0), new(u1, 1)];
            Color[] cols = [Colors.White, Colors.White, Colors.White, Colors.White];
            DrawPrimitive(pts, cols, uvs, tex);
        }
    }

    public override void _Draw()
    {
        // The outer ring: BuildRing(512, 32, 50 segments, radius 31, v 1), turned by -yaw.
        if (_showOuter)
        {
            Ring(_outer, 50, 0.31f, 32.0f * MathF.PI / 512.0f, _outerYaw);
        }

        // The inner ring: BuildRing(256, 8, 40 segments, radius 27, v 1) in the compass's projection (0.5 .. 100,
        // 640 * 0.00078125 by 480 * 0.00078125): x and y scale to 320 pixels per unit around the centre.
        const int Segments = 40;
        float t = InnerHeight * 1.0f * MathF.PI / InnerWidth;
        float r = 0.27f;
        float outer = (t + 1.0f) * r * 320.0f;
        float inner = (1.0f - t) * r * 320.0f;
        var centre = new Vector2(320, 240);
        for (int k = 0; k < Segments; k++)
        {
            float a0 = k * PiM2 / Segments;
            float a1 = (k + 1) * PiM2 / Segments;
            float u0 = (float)k / Segments;
            float u1 = (float)(k + 1) / Segments;
            Vector2 P(float a, float rad) => centre + new Vector2(MathF.Sin(a) * rad, -MathF.Cos(a) * rad);
            Vector2[] pts = [P(a0, inner), P(a0, outer), P(a1, outer), P(a1, inner)];
            Vector2[] uvs = [new(u0, 1), new(u0, 0), new(u1, 0), new(u1, 1)];
            Color[] cols = [Colors.White, Colors.White, Colors.White, Colors.White];
            DrawPrimitive(pts, cols, uvs, (Texture2D?)_inner);
        }
    }
}

/// <summary>Sprites round the compass ring, each centred at its angle and radius, turned and tinted.</summary>
public sealed partial class HudSprites(List<(float A, float Radius, float Turn, uint Colour, Texture2D? Tex)> sprites) : Node2D
{
    public override void _Draw()
    {
        var centre = new Vector2(320, 240);
        foreach ((float a, float radius, float turn, uint colour, Texture2D? tex) in sprites)
        {
            if (tex is null)
            {
                continue;
            }

            Vector2 at = centre + new Vector2(MathF.Sin(a) * radius, -MathF.Cos(a) * radius);
            var c = new Color(((colour >> 16) & 255) / 255f, ((colour >> 8) & 255) / 255f, (colour & 255) / 255f, (colour >> 24) / 255f);
            DrawSetTransform(at, turn, Vector2.One);
            DrawTexture(tex, -tex.GetSize() / 2, c);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }
}

/// <summary>
/// CSPRITERENDERER::DrawSprite sprites at screen positions: each anchored by its alignment (0 top left, 2 bottom left,
/// 4 centre), turned about its centre, tinted, and for a fill (SPT_FILL_RIGHT) cut to the left part of its width.
/// </summary>
public sealed partial class HudQuads(List<(float X, float Y, int Align, float Turn, uint Colour, Texture2D? Tex, float Fill)> sprites) : Node2D
{
    public override void _Draw()
    {
        foreach ((float x, float y, int align, float turn, uint colour, Texture2D? tex, float fill) in sprites)
        {
            if (tex is null)
            {
                continue;
            }

            Vector2 size = tex.GetSize();
            float left = x, top = y;
            switch (align)
            {
                case 2: top -= size.Y; break;
                case 4: left -= size.X * 0.5f; top -= size.Y * 0.5f; break;
            }

            var c = new Color(((colour >> 16) & 255) / 255f, ((colour >> 8) & 255) / 255f, (colour & 255) / 255f, (colour >> 24) / 255f);
            if (fill < 0.0f)
            {
                // SPT_FILL_LEFT: the right part a of the width, drawn mirrored (u = a at its left edge, 0 at the right).
                float a = -fill;
                Vector2[] pts = [new(left + size.X - (size.X * a), top), new(left + size.X, top), new(left + size.X, top + size.Y), new(left + size.X - (size.X * a), top + size.Y)];
                Vector2[] uvs = [new(a, 0), new(0, 0), new(0, 1), new(a, 1)];
                Color[] cs = [c, c, c, c];
                DrawPrimitive(pts, cs, uvs, tex);
                continue;
            }

            if (fill < 1.0f)
            {
                DrawTextureRectRegion(tex, new Rect2(left, top, size.X * fill, size.Y), new Rect2(0, 0, size.X * fill, size.Y), c);
                continue;
            }

            var centre = new Vector2(left, top) + (size / 2);
            DrawSetTransform(centre, turn, Vector2.One);
            DrawTexture(tex, -size / 2, c);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }
}

/// <summary>CHud's mUnknown1dc / mUnknown1ec for viewpoint 0: the heat bar's alpha and the value it eases to.</summary>
public sealed class AmmoEase
{
    public float Alpha;
    public float Target;
}
