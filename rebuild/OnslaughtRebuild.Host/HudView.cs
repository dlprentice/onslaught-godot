// SPDX-License-Identifier: GPL-3.0-or-later
// The HUD's compass, as retail draws it. Adapted from bea-decomp src/DXCompass.cpp (BuildRing 0053c1d0, the inner ring's
// texture Unknown53c510 0053c510, Render 0053cd30) and Compass.cpp (CCompass::Render 00427210: the bar lines and
// objective markers). The values are the parity core's battle engine.

using Godot;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
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
    private Texture2D? _barLine;
    private Texture2D? _objective;
    private readonly List<(float A, float Radius, float Turn, uint Colour, Texture2D? Tex)> _sprites = [];

    /// <summary>The compass for one render, from the battle engine's state.</summary>
    public static HudView Build(BattleEngine be, TextureLibrary textures, IEnumerable<Thing> objectives, ref int lastHealth, ref int lastEnergy)
    {
        var view = new HudView { Name = "Hud" };
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

        // A4R4G4B4 rows: row 0 and the last clear; row 1 (the outer edge) brightest, fading towards the inner edge.
        var image = Image.CreateEmpty(InnerWidth, InnerHeight, false, Image.Format.Rgba8);
        int row = 1;
        for (int n = InnerHeight - 2; n > 0; n--, row++)
        {
            int d = InnerHeight - 2;
            int alpha = 4 * n / d;
            Color C((int r, int g, int b) c) => new Color(c.r * n / d * 17 / 255f, c.g * n / d * 17 / 255f, c.b * n / d * 17 / 255f, alpha * 17 / 255f);
            for (int x = 0; x < InnerWidth; x++)
            {
                Color col = x < InnerWidth * 60 / 360 ? new Color(0, 0, 0, 0)
                    : x < health ? C(h0)
                    : x < InnerWidth * 150 / 360 ? C(h)
                    : x < InnerWidth * 225 / 360 ? new Color(0, 0, 0, 0)
                    : x < energy ? C(e)
                    : C(e0);
                image.SetPixel(x, row, col);
            }
        }

        view._inner = ImageTexture.CreateFromImage(image);
        view._barLine = textures.Get(@"hud\v2\BarLine.tga") ?? textures.Find(@"hud\v2\BarLine.tga", 0);
        view._objective = textures.Get(@"hud\v2\CompassObjectiveMarker.tga") ?? textures.Find(@"hud\v2\CompassObjectiveMarker.tga", 0);

        // The bar lines: each bar's moving end and its fixed start, at radius 110.
        foreach (int pos in new[] { health, energy, InnerWidth * 150 / 360, InnerWidth * 225 / 360 })
        {
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
        return view;
    }

    public override void _Draw()
    {
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
