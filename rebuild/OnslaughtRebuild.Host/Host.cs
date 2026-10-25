// SPDX-License-Identifier: GPL-3.0-only

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
        if (System.Environment.GetEnvironmentVariable("HOST_LIVE") == "flow")
        {
            try
            {
                ReadyFlow();
            }
            catch (Exception e)
            {
                GD.PrintErr($"HOST_FLOW_ERROR: {e}");
                GetTree().Quit(1);
            }
            return;
        }

        if (System.Environment.GetEnvironmentVariable("HOST_LIVE") == "1")
        {
            try
            {
                ReadyLive();
            }
            catch (Exception e)
            {
                FailLive(e);
                GetTree().Quit(1);
            }
            return;
        }

        GameFiles files = GameFiles.Find() ?? throw new InvalidOperationException("the game installation was not found (set ONSLAUGHT_GAME_DIR)");
        int present = int.TryParse(System.Environment.GetEnvironmentVariable("HOST_PRESENT"), out int n) ? n : 30;
        _capture = System.Environment.GetEnvironmentVariable("HOST_CAPTURE");

        var textures = new TextureLibrary(files.Read);
        var surf = new SurfView();
        // HOST_TAPE: a recorded input tape to replay (as the retail goldens do: with the virtual DirectSound voice model).
        string? tape = System.Environment.GetEnvironmentVariable("HOST_TAPE");
        LevelSession session = LevelSession.Load(100, files.Read, tape is null ? null : LevelSession.VirtualVoiceFor(files.Read),
            (tag, c) => textures.ReadChunk(tag, c) || surf.ReadChunk(tag, c));
        if (tape is not null)
        {
            session.Tape = OnslaughtRebuild.Parity.RetailTape.Load(tape);
        }

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

        // sCloudU and sCloudV advance by the render frame's length at each world render (unchecked: one Present each).
        GD.Print($"HOST: cloud drift {BitConverter.SingleToUInt32Bits(_cloudU):x8} {BitConverter.SingleToUInt32Bits(_cloudV):x8} after {_renders} world renders");
        int[] tileLevels = TerrainView.ChooseLevels(session.Context.Map, _landscapeView);
        GD.Print($"HOST: landscape texture levels {string.Join(" ", Enumerable.Range(0, 5).Select(l => $"L{l}:{tileLevels.Count(v => v == l)}"))}");
        world.AddChild(TerrainView.Build(session.Context.Map, terrain, textures, tileLevels, new Vector2(_cloudU, _cloudV)));
        GD.Print($"HOST: terrain detail {terrain.DetailTexture}, sky {terrain.KempyCube}, ambient {terrain.Ambient:x8} sun {terrain.Sun:x8} anti {terrain.AntiSun:x8}, fog {terrain.Fog:x8} density {terrain.FogDensity}; light out of range {terrain.LightOutOfRange}");
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
            // CBattleEngine::Render: the player's own battle engine is not drawn from its first-person view.
            if (thing is OnslaughtRebuild.Parity.Things.BattleEngine be && session.Context.Particles is { } lp && !lp.World.BattleEngineDrawn(be))
            {
                continue;
            }

            if (meshViews.Build(thing, session.Game.Loop.Fraction) is { } drawn)
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
            // The cockpit, in first-person view (draw_fairly_late_stuff).
            if (engine is OnslaughtRebuild.Parity.Things.BattleEngine be0 && !be0.IsDying
                && session.Game.GetPlayer(0)?.CurrentView == OnslaughtRebuild.Parity.Game.PlayerView.FirstPerson
                && be0.Cockpit is { ShouldRender: true, RenderThing: { MeshOrNull: not null } cockpitRt }
                && meshViews.BuildPosed(cockpitRt, session.Context.TransformStatics) is { } cockpitNode)
            {
                world.AddChild(cockpitNode);
            }

            // PostRender: HUD.Render's overlay for viewpoint 0 when its camera shows the HUD and its battle engine lives, then
            // the battle line whenever the camera shows the HUD.
            bool showHud = session.Game.GetPlayer(0) is not null && session.Game.GetCurrentCamera(0)?.GetShowHud() == true;
            if (showHud && engine is OnslaughtRebuild.Parity.Things.BattleEngine hudBe && !hudBe.IsDying)
            {
                _view!.AddChild(HudView.Build(hudBe, textures, session.Context.World.ObjectiveThings.ToList(), ref _lastHealth, ref _lastEnergy,
                    _dial ??= files.Read(@"data\Dial.raw").Bytes, session.Game.Loop.Fraction, session.Context.World.UnitNB.ToList(), _ammo,
                    session.Context.Particles?.RenderFrameLength ?? 1.0f));
            }

            if (showHud && session.Context.InfluenceMap is { } influence)
            {
                var nodes = influence.Nodes.ToList();
                _battleLine ??= BattleLineData.Build(session.Context.Map, nodes);
                _view!.AddChild(BattleLineView.Build(_battleLine, nodes, engine as OnslaughtRebuild.Parity.Things.BattleEngine,
                    session.Context.World.ObjectiveThings.ToList(), session.Context.World.SquadNB.ToList(), session.Context.Events.Time, textures,
                    OnslaughtRebuild.Parity.Script.ScriptRuntime.Of(session.Context).MessageBox?.Current is not null));
            }

            if (session.Context.Particles is { } particles)
            {
                world.AddChild(ParticleView.Build(particles.Manager, textures, pos, ori, session.Context.Events.Time));
                GD.Print($"HOST: {particles.Manager.NumLiveParticles} particles live");
            }

            GD.Print($"HOST: surf {surf.StripCount} strips; water scroll2 {BitConverter.SingleToUInt32Bits(_waterScroll):x8}");
            GD.Print($"HOST: camera {camera.GetType().Name} at {pos}, zoom {zoom}; battle engine at {engine?.Pos}");
        }
    }

    /// <summary>The render's view of the camera (the parity core's CInterpolatedCamera).</summary>
    public static (FVector Pos, FMatrix Ori, float Zoom) Interpolated(ParityCamera camera, float fraction) =>
        ParityCamera.RenderView(camera, fraction);

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
        if (_flowMode)
        {
            try
            {
                ProcessFlow(delta);
            }
            catch (Exception e)
            {
                _flowFailure = true;
                _flowStop = $"Presentation failed after Present {_flowLast?.Present}: {e.Message}";
                GD.PrintErr($"HOST_FLOW_FAILED: {e}");
                FinishFlow();
                GetTree().Quit(1);
            }
            return;
        }

        if (_liveMode)
        {
            try
            {
                ProcessLive(delta);
            }
            catch (Exception e)
            {
                FailLive(e);
            }
            return;
        }

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

    private float _cloudU;
    private float _cloudV;
    private float _waterAngle;
    private int _lastHealth = int.MinValue;
    private byte[]? _dial;
    private BattleLineData? _battleLine;
    private readonly AmmoEase _ammo = new();
    private int _lastEnergy = int.MinValue;
    private readonly TerrainView.LandscapeView _landscapeView = new();
    private float _waterScroll;
    private int _renders;

    /// <summary>
    /// CGame::Render's frame length (game.cpp:1669..1685: (time + fraction * CLOCK_TICK) / CLOCK_TICK, less the last; 1
    /// at the first render), CDXLandscape's cloud drift (DXLandscape.cpp:558..563) and CDXWater's angle2 (WATER_ADVANCE),
    /// for each Present that draws the world. Loading Presents draw no landscape.
    /// </summary>
    private void AccumulateCloudDrift(LevelSession session)
    {
        if (session.Context.RenderFrameNumber == 0)
        {
            return;
        }

        // StartRender already computed the compiled reciprocal expression, including its rounding and history.
        // Recomputing it with division changes the phase; a loading Present has not run StartRender at all.
        float length = session.Context.Particles?.RenderFrameLength
            ?? throw new InvalidOperationException("a world render has no canonical particle frame length");
        _cloudU += length * 0.001f;
        if (_cloudU > 1.0f)
        {
            _cloudU -= 1.0f;
        }

        _waterAngle = WaterView.Advance(_waterAngle, length);
        if (session.Game.GetCurrentCamera(0) is { } cam)
        {
            (FVector camPos, FMatrix camOri, _) = Interpolated(cam, session.Game.Loop.Fraction2);
            _landscapeView.Advance(camPos, camOri);
        }

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
