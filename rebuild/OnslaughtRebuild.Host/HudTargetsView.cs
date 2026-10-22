// SPDX-License-Identifier: GPL-3.0-only
// The HUD's passes at the world's targets, as retail draws them. Adapted from bea-decomp src/Hud.cpp (RenderWorldTargets
// 00486e00, RenderTargetMarkers 00484340 with its Ease and LerpColour, Render00485d50 00485d50's guns panel, DimColour)
// and BattleEngine.cpp (GetInterpolatedAutoAimPos 0040d7c0, GetInterpolatedTrackingPos 0040da30). The values are the
// parity core's; the objectives' and speaker's points are those its HUD render took (WorldRender.RenderHud), so drawing
// evaluates no pose.

using Godot;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Host;

/// <summary>
/// CHud's eased crosshair state for viewpoint 0, kept from render to render (Reset's values): mUnknown0d4 (what the
/// crosshair is over: 0 an enemy, 1 nothing, 4 a friend), mUnknown0e4 (the inner rings' scale, 0.5 when locked),
/// mUnknown0f4 (the aug's spin), mUnknown190 (the speaker marker's frame) and mUnknown1b8 (the lock ring's spin).
/// </summary>
public sealed class HudTargetState
{
    public float Over = 1.0f;
    public float Lock = 1.0f;
    public float Spin;
    public float SpeakerFrame;
    public float LockSpin;
}

/// <summary>
/// The HUD passes that follow the world (RenderOverlayForViewpoint's first and its crosshair): each objective's screen
/// marker in front of the camera, added, coloured by its side and faded when near (RenderWorldTargets; the lock-on pieces
/// and the radar warning receiver's threats are not drawn, as neither locks nor the receiver are ported); the crosshair at
/// the interpolated auto-aim point, tinted and turned by what it is over and shrunk while locked on (RenderTargetMarkers),
/// with the tracking predictor and a marker over a speaking unit; and the guns panel (Render00485d50's sprites; its text,
/// and the HUD components scripts may hide, are not drawn yet).
/// </summary>
public static class HudTargetsView
{
    private const float PiM2 = 6.28318530717958f;

    /// <param name="project">The level camera's screen point for a world point, or null behind it.</param>
    public static (Node2D WorldTargets, Node2D Crosshair, Node2D Guns) Build(BattleEngine be, Camera camera,
        IReadOnlyList<(Thing Thing, FVector Pos)> objectives, FVector? speakerPoint, HudTargetState state, float renderFrameLength, float fraction, float eventTime,
        Func<FVector, Vector2?> project, TextureLibrary textures)
    {
        Texture2D? Tex(string name) => textures.Get(name) ?? textures.Find(name, 0);

        // RenderWorldTargets: the lock ring's spin, then the objectives' markers, added.
        state.LockSpin += renderFrameLength * 0.05f;
        if (state.LockSpin >= PiM2)
        {
            state.LockSpin -= PiM2;
        }

        FVector camPos = camera.GetPos();
        FVector camFwd = camera.GetOrientation().Transform(new FVector(0, 1, 0));
        Texture2D? marker = Tex(@"hud\v3\ScreenMarker.tga");
        var markers = new List<(Vector2, float, uint, Texture2D?, float, float)>();
        foreach ((Thing thing, FVector pos) in objectives)
        {
            FVector d = pos - camPos;
            bool behind = (camFwd.X * d.X) + (camFwd.Y * d.Y) + (camFwd.Z * d.Z) < 0.001f;
            uint col = 0xff3fafff;
            if (thing.IsA(ThingType.Unit) && thing is Unit unit)
            {
                col = unit.Allegiance switch
                {
                    0 => 0xff6fff2fu,
                    1 => 0xffff6f2fu,
                    _ => col,
                };
            }
            else if (thing.IsA(ThingType.Feature) && thing is Feature feature)
            {
                col = feature.Allegiance switch
                {
                    0 => 0xff6fff2fu,
                    1 => 0xffff6f2fu,
                    _ => col,
                };
            }

            if (behind || project(pos) is not { } screen)
            {
                continue;
            }

            float size = MathF.Sqrt((d.X * d.X) + (d.Y * d.Y) + (d.Z * d.Z));
            size = thing.GetRadius() / size * 5.0f;
            size /= camera.GetZoom();
            if (size < 0.2f)
            {
                size = 0.2f;
            }
            else if (size > 0.5f)
            {
                int a = 255 - (int)((size - 0.5f) * 85.0f);
                col = DimColour(col, a < 0 ? 0 : a);
            }

            markers.Add((screen, MathF.PI / 4, col, marker, size, size));
        }

        var worldTargets = new HudScaledSprites(markers)
        {
            Name = "WorldTargets",
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };

        // RenderTargetMarkers: the tracking predictor, then the crosshair at the interpolated auto-aim point.
        var cross = new List<(Vector2, float, uint, Texture2D?, float, float)>();
        if (be.TrackingActive && project(InterpolatedTrackingPos(be, fraction)) is { } predictor)
        {
            cross.Add((predictor, 0.0f, 0xffffffffu, Tex(@"hud\Crosshair Predictor.tga"), 1.0f, 1.0f));
        }

        Vector2 aim = project(InterpolatedAutoAimPos(be, camera, fraction)) ?? new Vector2(320, 240);
        int over = 0;
        if (be.AutoAimTarget.ToRead is not null && !be.TrackingActive)
        {
            over = 2;
        }
        else if (be.CurrentUnitOverCrosshairRegardlessOfRange.ToRead is { } unit && unit.IsA(ThingType.Unit))
        {
            int side = be.Allegiance, other = unit.Allegiance;
            if (side == 0)
            {
                over = other == 0 ? 1 : other == 1 ? 2 : 0;
            }
            else if (side == 1)
            {
                over = other == side ? 1 : other == 0 ? 2 : 0;
            }
        }

        Thing? marked = be.AutoAimTarget.ToRead ?? be.CurrentUnitOverCrosshair.ToRead;
        bool locked = !be.TrackingActive && marked is not null && over == 2;
        float half = renderFrameLength * 0.5f;
        Ease(ref state.Over, over switch { 0 => 1.0f, 1 => 4.0f, _ => 0.0f }, half, 0.05f);
        Ease(ref state.Lock, locked ? 0.5f : 1.0f, half * 0.6f, 0.01f);

        uint colour = state.Over < 1.0f
            ? LerpColour(0xafff7f7f, 0xafffffff, state.Over)
            : LerpColour(0xaf9fff9f, 0xafffffff, (4.0f - state.Over) / 3.0f);
        float scale = (state.Over * 0.5f) + 0.5f;
        uint colour2 = colour;
        if (state.Over > 1.0f)
        {
            int a = (int)((4.0f - state.Over) * 68.0f + 51.0f);
            colour2 = BlendAlpha2(colour, Math.Clamp(a, 0, 0xff));
        }

        if (state.Spin != 0 || be.AugActive)
        {
            state.Spin += renderFrameLength * 0.25f;
        }

        if (state.Spin > PiM2)
        {
            state.Spin = be.AugActive ? state.Spin - PiM2 : 0;
        }

        if (be.AugActive)
        {
            float pulse = (MathF.Sin(state.Spin * 2) * 0.5f) + 0.5f;
            colour2 = LerpColour(0xaf1fafff, 0xafffcf7f, pulse);
        }

        float turn = state.Over < 1.0f ? (1.0f - state.Over) * 0.5f : 0.0f;
        cross.Add((aim, turn + state.Spin, colour2, Tex(@"hud\v3\hud_crosshair_primary.tga"), scale, scale));
        float inner = state.Lock;
        uint colour3 = BlendAlpha2(colour, Math.Clamp((int)(((1.0f - state.Lock) + 1.0f) * 127.5f), 0, 0xff));
        cross.Add((aim, 0.0f, colour3, Tex(@"hud\v3\hud_crosshair_secondary.tga"), inner, inner));
        cross.Add((aim, 0.0f, colour3, Tex(@"hud\v3\hud_crosshair_dot.tga"), inner, inner));

        // A marker over the speaking unit (its frame cycling through six) while it is ahead of the battle engine.
        state.SpeakerFrame += renderFrameLength * 0.4f;
        if (state.SpeakerFrame >= 6.0f)
        {
            state.SpeakerFrame -= 6.0f;
        }

        if (speakerPoint is { } spoken)
        {
            float yaw = -MathF.Atan2(spoken.X - be.Pos.X, spoken.Y - be.Pos.Y);
            if (MathF.Abs(AngleDifference(yaw, be.CurrentOrientation.Yaw)) < MathF.PI / 2 && project(spoken) is { } at)
            {
                cross.Add((at, 0.0f, 0xffffff00u, Tex($@"hud\speaker{(int)MathF.Floor(state.SpeakerFrame)}.tga"), 1.0f, 1.0f));
            }
        }

        var crosshair = new HudScaledSprites(cross)
        {
            Name = "Crosshair",
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mix },
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };

        // Render00485d50's guns panel: the darkener alpha-blended, then added the outline and the current weapon's mount
        // (side, front or top; all three without a weapon), pulsing in red.
        var guns = new Node2D { Name = "Guns" };
        if (be.CountActiveWeapons() > 0)
        {
            const float X = 112.0f, Y = 480.0f - 74.0f;
            guns.AddChild(new HudQuads([(X, Y, 0, 0.0f, 0x5f000000u, Tex(@"hud\v2\GunsDarken.tga"), 1.0f)])
            {
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mix },
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            });
            uint pulse = unchecked(0xff7f3f2fu - (uint)((int)((MathF.Sin(eventTime * 4.0f) + 1.0f) * -64.0f) << 16));
            var lit = new List<(float, float, int, float, uint, Texture2D?, float)> { (X, Y, 0, 0.0f, 0xff7f7f7fu, Tex(@"hud\v2\GunsOutline.tga"), 1.0f) };
            string[] mounts = be.GetCurrentWeapon()?.Data.Placement switch
            {
                0 => [@"hud\v2\GunsSide.tga"],
                1 => [@"hud\v2\GunsFront.tga"],
                2 => [@"hud\v2\GunsTop.tga"],
                _ => [@"hud\v2\GunsSide.tga", @"hud\v2\GunsFront.tga", @"hud\v2\GunsTop.tga"],
            };
            foreach (string mount in mounts)
            {
                lit.Add((X, Y, 0, 0.0f, pulse, Tex(mount), 1.0f));
            }

            guns.AddChild(new HudQuads(lit)
            {
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            });
        }

        return (worldTargets, crosshair, guns);
    }

    /// <summary>GetInterpolatedAutoAimPos: one unit from the interpolated view along the interpolated auto-aim offsets.</summary>
    private static FVector InterpolatedAutoAimPos(BattleEngine be, Camera camera, float f)
    {
        FVector pos = ((camera.GetPos() - camera.GetOldPos()) * f) + camera.GetOldPos();
        FMatrix ori = camera.GetOrientation().Add(camera.GetOldOrientation().Scale(-1.0f)).Scale(f).Add(camera.GetOldOrientation());
        float yaw = ((be.AutoAimYawOffset - be.OldAutoAimYawOffset) * f) + be.OldAutoAimYawOffset;
        float pitch = ((be.AutoAimPitchOffset - be.OldAutoAimPitchOffset) * f) + be.OldAutoAimPitchOffset;
        return pos + ori.Multiply(FMatrix.MakeRotationOutOfLine(yaw, pitch, 0.0f)).Transform(new FVector(0, 1, 0));
    }

    /// <summary>GetInterpolatedTrackingPos: one unit from the interpolated engine along the interpolated tracking offsets.</summary>
    private static FVector InterpolatedTrackingPos(BattleEngine be, float f)
    {
        FVector pos = ((be.Pos - be.OldPos) * f) + be.OldPos;
        FMatrix ori = be.Orientation.Add(be.OldOrientation.Scale(-1.0f)).Scale(f).Add(be.OldOrientation);
        float yaw = ((be.TrackingYawOffset - be.OldTrackingYawOffset) * f) + be.OldTrackingYawOffset;
        float pitch = ((be.TrackingPitchOffset - be.OldTrackingPitchOffset) * f) + be.OldTrackingPitchOffset;
        return pos + ori.Multiply(FMatrix.MakeRotationOutOfLine(yaw, pitch, 0.0f)).Transform(new FVector(0, 1, 0));
    }

    private static void Ease(ref float value, float target, float rate, float snap)
    {
        value += rate * (target - value);
        if (MathF.Abs(target - value) < snap)
        {
            value = target;
        }
    }

    private static uint LerpColour(uint from, uint to, float t)
    {
        int Channel(int shift) => (int)((((from >> shift) & 0xff) * (1.0f - t)) + (((to >> shift) & 0xff) * t));
        return (uint)((((((Channel(24) << 8) + Channel(16)) << 8) + Channel(8)) << 8) + Channel(0));
    }

    private static uint BlendAlpha2(uint col, int alpha) => (col & 0x00ffffff) | (unchecked((col >> 24) * (uint)alpha << 16) & 0xff000000);

    private static uint DimColour(uint col, int a)
    {
        uint r = (uint)(((col >> 16) & 0xff) * a) >> 8, g = (uint)(((col >> 8) & 0xff) * a) >> 8, b = (uint)((col & 0xff) * a) >> 8;
        return (col & 0xff000000) + (((r << 8) + g) << 8) + b;
    }

    private static float AngleDifference(float a, float b)
    {
        float d = a - b;
        while (d > MathF.PI) d -= PiM2;
        while (d < -MathF.PI) d += PiM2;
        return d;
    }
}

/// <summary>CSPRITERENDERER::DrawSprite sprites centred at screen points (SA_CENTRE), each turned, tinted and scaled.</summary>
public sealed partial class HudScaledSprites(List<(Vector2 At, float Turn, uint Colour, Texture2D? Tex, float ScaleX, float ScaleY)> sprites) : Node2D
{
    public override void _Draw()
    {
        foreach ((Vector2 at, float turn, uint colour, Texture2D? tex, float sx, float sy) in sprites)
        {
            if (tex is null)
            {
                continue;
            }

            var c = new Color(((colour >> 16) & 255) / 255f, ((colour >> 8) & 255) / 255f, (colour & 255) / 255f, (colour >> 24) / 255f);
            DrawSetTransform(at, turn, new Vector2(sx, sy));
            DrawTexture(tex, -tex.GetSize() / 2, c);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }
}
