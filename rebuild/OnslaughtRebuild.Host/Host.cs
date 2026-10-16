// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Things;
using ParityCamera = OnslaughtRebuild.Parity.Game.Camera;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The parity core's host. This first slice runs level 100 in the parity core Present by Present, as the retail harness
/// starts it (HOST_PRESENT, default 30), draws its terrain and things from that state, looks from the player's current
/// camera as retail's engine does (CInterpolatedCamera: the camera's last two updates mixed by the render fraction),
/// and (with HOST_CAPTURE set) writes the 640x480 frame to a PNG and quits.
/// </summary>
public partial class Host : Node
{
    private SubViewport? _view;
    private string? _capture;
    private int _frames;

    /// <summary>Retail's world (x east, y north, z down) in Godot's (y up, right-handed): (x, -z, -y).</summary>
    public static Vector3 ToGodot(FVector v) => new(v.X, -v.Z, -v.Y);

    public override void _Ready()
    {
        GameFiles files = GameFiles.Find() ?? throw new InvalidOperationException("the game installation was not found (set ONSLAUGHT_GAME_DIR)");
        int present = int.TryParse(System.Environment.GetEnvironmentVariable("HOST_PRESENT"), out int n) ? n : 30;
        _capture = System.Environment.GetEnvironmentVariable("HOST_CAPTURE");

        var textures = new TextureLibrary(files.Read);
        var surf = new SurfView();
        LevelSession session = LevelSession.Load(100, files.Read, otherChunk: (tag, c) => textures.ReadChunk(tag, c) || surf.ReadChunk(tag, c));
        GD.Print($"HOST: {textures.Count} textures read");
        try
        {
            while (session.PresentCount < present)
            {
                session.AdvanceToNextPresent();
                AccumulateCloudDrift(session);
            }
        }
        catch (NotSupportedException e)
        {
            GD.Print($"HOST: the parity core stopped after Present {session.PresentCount}: {e.Message}");
        }

        GD.Print($"HOST: level 100 at Present {session.PresentCount}, time {session.Context.Events.Time}, state {session.Game.State}, fraction {session.Game.Loop.Fraction2}");

        _view = new SubViewport { Size = new Vector2I(640, 480), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(_view);
        var world = new Node3D();
        _view.AddChild(world);
        var engineChunk = new OnslaughtRebuild.Parity.Data.ChunkReader(session.Resources.OpenAt(session.Resources.EngineResourcesPosition));
        Landscape.TerrainTexture terrain = Landscape.TerrainTexture.Read(engineChunk);
        // Retail's lit cache at level 100 is 512 x 512 (shift 0: the port's d3dlog of Present 150, tex0 of the landscape).
        int shift = int.TryParse(System.Environment.GetEnvironmentVariable("HOST_TERRAIN_SHIFT"), out int ts) ? ts : 0;

        // sCloudU and sCloudV advance by the render frame's length at each world render (unchecked: one Present each).
        GD.Print($"HOST: cloud drift {BitConverter.SingleToUInt32Bits(_cloudU):x8} {BitConverter.SingleToUInt32Bits(_cloudV):x8} after {_renders} world renders");
        world.AddChild(TerrainView.Build(session.Context.Map, terrain, textures, shift, new Vector2(_cloudU, _cloudV)));
        GD.Print($"HOST: terrain at shift {shift}, detail {terrain.DetailTexture}, sky {terrain.KempyCube}, ambient {terrain.Ambient:x8} sun {terrain.Sun:x8} anti {terrain.AntiSun:x8}, fog {terrain.Fog:x8} density {terrain.FogDensity}; light out of range {terrain.LightOutOfRange}");
        world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, 30, 0) });

        // D3D's table fog, EXP mode (PCLTShell's fog state): exp(-density * depth) towards the map's fog colour. Godot's
        // exponential fog uses the distance rather than the depth.
        uint fog = terrain.Fog;
        world.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Colors.Black,
                FogEnabled = true,
                FogMode = Godot.Environment.FogModeEnum.Exponential,
                FogLightColor = new Color(((fog >> 16) & 255) / 255f, ((fog >> 8) & 255) / 255f, (fog & 255) / 255f),
                FogDensity = terrain.FogDensity,
                FogSkyAffect = 0,
            },
        });

        Thing? engine = session.Game.GetPlayer(0)?.BattleEngine;
        var meshViews = new MeshView(textures, terrain);
        int meshes = 0;
        foreach (Thing thing in session.Context.World.Things.ToList())
        {
            if (meshViews.Build(thing) is { } drawn)
            {
                world.AddChild(drawn);
                meshes++;
            }
        }

        GD.Print($"HOST: {meshes} things drawn with meshes of {session.Context.World.Things.Size}; {textures.Missing} textures not found");

        ParityCamera? camera = session.Game.GetCurrentCamera(0);
        var view = new Camera3D { KeepAspect = Camera3D.KeepAspectEnum.Width, Near = 0.1f, Far = 20000 };
        world.AddChild(view);
        if (camera is not null)
        {
            (FVector pos, FMatrix ori, float zoom) = Interpolated(camera, session.Game.Loop.Fraction2);
            Place(view, pos, ori, zoom);
            world.AddChild(SkyView.Build(terrain.KempyCube, textures, pos));
            if (surf.Build(terrain, textures, pos, ori, _waterAngle, _waterScroll) is { } surfMesh)
            {
                world.AddChild(surfMesh);
            }

            if (System.Environment.GetEnvironmentVariable("HOST_NO_GRID") != "1")
            {
                world.AddChild(WaterView.Build(terrain, session.Context.Map.WaterLevel, textures, pos, ori, _waterAngle));
            }
            GD.Print($"HOST: surf {surf.StripCount} strips; water scroll2 {BitConverter.SingleToUInt32Bits(_waterScroll):x8}");
            GD.Print($"HOST: camera {camera.GetType().Name} at {pos}, zoom {zoom}; battle engine at {engine?.Pos}");
        }
    }

    /// <summary>
    /// CInterpolatedCamera (0041ad30): position, orientation and zoom from the camera's previous update towards its
    /// current one by the render fraction (the orientation's rows renormalised). Only the pan camera keeps its previous
    /// update so far; the others are drawn where they are.
    /// </summary>
    public static (FVector Pos, FMatrix Ori, float Zoom) Interpolated(ParityCamera camera, float fraction)
    {
        FVector pos = camera.GetPos();
        FMatrix ori = camera.GetOrientation();
        if (camera is PanCamera pan)
        {
            FVector old = pan.OldPos;
            pos = old + ((pos - old) * fraction);
            FMatrix o = pan.OldOrientation;
            FVector Row(FVector a, FVector b)
            {
                FVector r = a + ((b - a) * fraction);
                float m = MathF.Sqrt((r.X * r.X) + (r.Y * r.Y) + (r.Z * r.Z));
                return m > 0 ? r * (1 / m) : r;
            }

            ori = new FMatrix(Row(o.Row0, ori.Row0), Row(o.Row1, ori.Row1), Row(o.Row2, ori.Row2));
        }

        return (pos, ori, camera.GetZoom());
    }

    /// <summary>
    /// CEngine::GetViewMatrixFromCamera's view: the orientation's local Y looks forward and local -Z is up (Z is down);
    /// the projection's half-width is the zoom at depth 1 (CRenderInfo::SetProjection), so the horizontal view is
    /// 2 atan(zoom).
    /// </summary>
    public static void Place(Camera3D view, FVector pos, FMatrix ori, float zoom)
    {
        FVector forward = ori.Transform(new FVector(0, 1, 0));
        FVector up = ori.Transform(new FVector(0, 0, -1));
        view.Position = ToGodot(pos);
        view.LookAt(ToGodot(pos + forward), ToGodot(up));
        view.Fov = Mathf.RadToDeg(2 * MathF.Atan(zoom));
    }

    public override void _Process(double delta)
    {
        if (_capture is null || _view is null)
        {
            return;
        }

        // A few frames for the viewport to render, then the capture.
        if (++_frames == 4)
        {
            Image image = _view.GetTexture().GetImage();
            Error error = image.SavePng(_capture);
            GD.Print($"HOST_CAPTURED {_capture} {error}");
            GetTree().Quit();
        }
    }

    private float _lastRenderFrameTime = -1.0f;
    private float _cloudU;
    private float _cloudV;
    private float _waterAngle;
    private float _waterScroll;
    private int _renders;

    /// <summary>
    /// CGame::Render's frame length (game.cpp:1669..1685: (time + fraction * CLOCK_TICK) / CLOCK_TICK, less the last; 1
    /// at the first render), CDXLandscape's cloud drift (DXLandscape.cpp:558..563) and CDXWater's angle2 (WATER_ADVANCE),
    /// for each Present that draws the world. The loading screen's Present (3) draws no landscape.
    /// </summary>
    private void AccumulateCloudDrift(LevelSession session)
    {
        if (session.PresentCount <= 3)
        {
            return;
        }

        float newTime = (session.Context.Events.Time + (session.Game.Loop.Fraction * 0.05f)) / 0.05f;
        float length = _lastRenderFrameTime != -1.0f ? newTime - _lastRenderFrameTime : 1.0f;
        _lastRenderFrameTime = newTime;
        _cloudU += length * 0.001f;
        if (_cloudU > 1.0f)
        {
            _cloudU -= 1.0f;
        }

        _waterAngle = WaterView.Advance(_waterAngle, length);
        _waterScroll += length * 0.003f;
        if (_waterScroll > 1.0f)
        {
            _waterScroll -= 1.0f;
        }

        _cloudV += length * 0.0005f;
        if (_cloudV > 1.0f)
        {
            _cloudV -= 1.0f;
        }

        _renders++;
    }
}
