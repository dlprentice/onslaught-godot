// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Host;

public partial class Host
{
    private bool _liveMode;
    private bool _liveDraw;
    private LiveHostTrace? _liveTrace;
    private LevelSession? _liveSession;
    private GameFiles? _liveFiles;
    private TextureLibrary? _liveTextures;
    private HostInputAdapter? _liveInput;
    private Window? _liveWindow;
    private Node3D? _liveWorld;
    private Camera3D? _liveCamera;
    private Node3D? _liveSky;
    private TerrainLiveView? _liveTerrain;
    private MeshView.LiveSet? _liveMeshes;
    private WaterView.LiveView? _liveWater;
    private SurfView.LiveView? _liveSurf;
    private HudView.TextureCache? _liveHudTextures;
    private TransientHostView? _liveHud;
    private TransientHostView? _liveBattleLine;
    private TransientHostView? _liveParticles;
    private Label? _liveBanner;
    private Label? _liveNotice;
    private bool _liveReplay;
    private bool _liveFocused;
    private bool _liveOwnsMouse;
    private bool _liveDisposed;
    private bool _liveFinished;
    private bool _liveFailure;
    private string? _liveStop;
    private long? _liveEndPresent;
    private double _liveAccumulator;
    private double _liveDiscardedSeconds;
    private Input.MouseModeEnum _livePreviousMouseMode;

    /// <summary>
    /// Opt-in Level 100 presentation, not the front-end/career route. The same parity session owns input, rendering
    /// callbacks and simulation. Godot paces fixed Presents; it never supplies a variable simulation delta.
    /// HOST_TAPE is a replay viewer and never mixes physical input with its recorded events. HOST_END_PRESENT
    /// optionally bounds a run and takes HOST_CAPTURE at that final state after four host render frames.
    /// </summary>
    private void ReadyLive()
    {
        _liveMode = true;
        _liveDraw = System.Environment.GetEnvironmentVariable("HOST_DRAW") != "0";
        _capture = System.Environment.GetEnvironmentVariable("HOST_CAPTURE");
        if (!_liveDraw && _capture is not null)
        {
            throw new ArgumentException("HOST_CAPTURE requires drawing; remove HOST_DRAW=0");
        }

        string? tape = System.Environment.GetEnvironmentVariable("HOST_TAPE");
        _liveReplay = tape is not null;
        if (!_liveDraw && !_liveReplay)
        {
            throw new ArgumentException("HOST_DRAW=0 requires HOST_TAPE; physical input requires the visible preview");
        }

        int present = int.TryParse(System.Environment.GetEnvironmentVariable("HOST_PRESENT"), out int n) ? Math.Max(n, 3) : 3;
        if (long.TryParse(System.Environment.GetEnvironmentVariable("HOST_END_PRESENT"), out long end) && end >= present)
        {
            _liveEndPresent = end;
        }
        else if (System.Environment.GetEnvironmentVariable("HOST_END_PRESENT") is not null)
        {
            throw new ArgumentException("HOST_END_PRESENT must be at least the initial HOST_PRESENT (default 3)");
        }

        _liveFiles = GameFiles.Find() ?? throw new InvalidOperationException("the game installation was not found (set ONSLAUGHT_GAME_DIR)");
        _liveTextures = new TextureLibrary(_liveFiles.Read);
        var surf = new SurfView();
        // Virtual voice preserves the measured message/mission timing. This slice emits no audio; the UI says so.
        _liveSession = LevelSession.Load(100, _liveFiles.Read, LevelSession.VirtualVoiceFor(_liveFiles.Read),
            (tag, c) => _liveTextures.ReadChunk(tag, c) || surf.ReadChunk(tag, c));
        if (tape is not null)
        {
            _liveSession.Tape = OnslaughtRebuild.Parity.RetailTape.Load(tape);
        }

        if (System.Environment.GetEnvironmentVariable("HOST_TRACE") is { } trace)
        {
            _liveTrace = new LiveHostTrace(trace, _liveDraw, _liveReplay);
        }

        while (_liveSession.PresentCount < present && !_liveSession.Ended && _liveStop is null)
        {
            AdvanceLiveCore();
            if (_liveSession.PresentCount < present)
            {
                _liveTrace?.Write(_liveSession);
            }
        }

        if (_liveDraw)
        {
            BuildLiveScene(surf);
        }

        _liveWindow = GetWindow();
        _liveWindow.FocusEntered += LiveFocusEntered;
        _liveWindow.FocusExited += LiveFocusExited;
        _liveFocused = _liveWindow.HasFocus();
        if (!_liveReplay)
        {
            // Even focus-release adapters mutate held/queued platform input. Replay therefore has no adapter,
            // so losing window focus cannot clear keys or relative motion delivered by the tape.
            _liveInput = new HostInputAdapter(_liveSession.Input.Input, _liveSession.Input.Mouse);
            _liveInput.SetFocused(_liveFocused);
        }
        UpdateLivePresentation();
        _liveTrace?.Write(_liveSession);
        CheckLiveEnd();
        UpdateLiveNotice();
        GD.Print($"HOST_LIVE_READY level=100 present={_liveSession.PresentCount} replay={_liveReplay} silent=true focused={_liveFocused} drawing={_liveDraw}");
    }

    private void BuildLiveScene(SurfView surf)
    {
        LevelSession session = _liveSession!;
        TextureLibrary textures = _liveTextures!;
        _view = new SubViewport { Size = new Vector2I(640, 480), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(_view);
        _liveWorld = new Node3D { Name = "LevelWorld" };
        _view.AddChild(_liveWorld);
        var chunk = new ChunkReader(session.Resources.OpenAt(session.Resources.EngineResourcesPosition));
        Landscape.TerrainTexture terrain = Landscape.TerrainTexture.Read(chunk);
        int[] levels = TerrainView.ChooseLevels(session.Context.Map, _landscapeView);
        _liveTerrain = TerrainView.BuildLive(session.Context.Map, terrain, textures, levels, new Vector2(_cloudU, _cloudV));
        _liveWorld.AddChild(_liveTerrain.Root);
        _liveWorld.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, 30, 0) });
        uint fog = terrain.Fog;
        _liveWorld.AddChild(new WorldEnvironment
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
        _liveCamera = new Camera3D { KeepAspect = Camera3D.KeepAspectEnum.Width, Near = 0.1f, Far = 20000 };
        _liveWorld.AddChild(_liveCamera);
        _liveMeshes = new MeshView(textures, terrain).CreateLive(_liveWorld);
        _liveHudTextures = new HudView.TextureCache();
        if (session.Game.GetCurrentCamera(0) is { } camera)
        {
            (FVector pos, FMatrix ori, _) = Interpolated(camera, session.Game.Loop.Fraction2);
            _liveSky = SkyView.Build(terrain.KempyCube, textures, pos);
            _liveWorld.AddChild(_liveSky);
            _liveSurf = surf.CreateLive(terrain, textures, pos, ori, _waterAngle, _waterScroll);
            if (_liveSurf is not null)
            {
                _liveWorld.AddChild(_liveSurf.Node);
            }

            if (System.Environment.GetEnvironmentVariable("HOST_NO_GRID") != "1")
            {
                _liveWater = WaterView.CreateLive(terrain, session.Context.Map.WaterLevel, textures, pos, ori, _waterAngle);
                _liveWorld.AddChild(_liveWater.Node);
            }
        }

        // The snapshot mode intentionally has no onscreen compositor. Live mode displays its viewport instead of
        // advancing an invisible simulation. Fixed 640x480 is also the input adapter's window coordinate system.
        var image = new TextureRect
        {
            Name = "LevelImage",
            Texture = _view.GetTexture(),
            Position = Vector2.Zero,
            Size = new Vector2(640, 480),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        };
        AddChild(image);
        _liveBanner = new Label
        {
            Name = "PreviewStatus",
            Position = new Vector2(8, 6),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _liveBanner.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _liveBanner.AddThemeConstantOverride("shadow_offset_x", 1);
        _liveBanner.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(_liveBanner);
        _liveNotice = new Label
        {
            Name = "PreviewNotice",
            Position = new Vector2(32, 180),
            Size = new Vector2(576, 120),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _liveNotice.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.85f) });
        AddChild(_liveNotice);

    }

    public override void _Input(InputEvent @event)
    {
        if (_liveMode && !_liveReplay && _liveStop is null && _liveFocused && _liveInput is { } adapter
            && _liveSession is { } session && adapter.Observe(@event, menuNavigationEnabled: !session.Game.Paused))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private void LiveFocusEntered()
    {
        _liveFocused = true;
        _liveAccumulator = 0;
        _liveInput?.SetFocused(!_liveReplay && _liveStop is null);
        UpdateLiveNotice();
    }

    private void LiveFocusExited()
    {
        _liveFocused = false;
        _liveAccumulator = 0;
        _liveInput?.SetFocused(false);
        UpdateLiveNotice();
    }

    private void ProcessLive(double delta)
    {
        if (_liveSession is null || _liveDisposed)
        {
            return;
        }

        if (_liveStop is null && (_liveReplay || _liveFocused))
        {
            // Bound catch-up work. If rendering cannot keep pace the preview runs slower; core time still advances
            // by exactly ReplayDt per Present. Never fast-forward through time while unfocused.
            double accepted = Math.Clamp(delta, 0, PlatformClock.ReplayDt * 4);
            _liveDiscardedSeconds += Math.Max(0, delta - accepted);
            _liveAccumulator += accepted;
            int steps = 0;
            while (_liveAccumulator >= PlatformClock.ReplayDt && steps++ < 4 && _liveStop is null)
            {
                _liveAccumulator -= PlatformClock.ReplayDt;
                if (!_liveReplay)
                {
                    _liveInput!.DrainBeforePresent();
                }

                long before = _liveSession.PresentCount;
                bool wasPaused = _liveSession.Game.Paused;
                AdvanceLiveCore();
                if (!_liveReplay && wasPaused != _liveSession.Game.Paused)
                {
                    // A batch containing Escape plus another key/button must not leave held/once input waiting
                    // for the menu's delayed admission, nor carry pending relative motion across Shutdown/Init.
                    _liveInput!.ReleaseAll();
                }
                if (_liveSession.PresentCount != before)
                {
                    // Presentation has history (HUD bars/ammo easing), so every Present updates it, even if Godot
                    // will display only the last of several catch-up frames. Core Render callbacks already ran.
                    UpdateLivePresentation();
                    _liveTrace?.Write(_liveSession);
                }

                CheckLiveEnd();
                UpdateLiveNotice();
            }
        }

        bool settle = _liveEndPresent is null || _liveStop is not null;
        if (settle && (_capture is not null || _liveEndPresent is not null) && ++_frames == 4)
        {
            int exitCode = _liveFailure ? 1 : 0;
            if (_capture is not null && _view is not null)
            {
                using Image image = _view.GetTexture().GetImage();
                Error error = image.SavePng(_capture);
                GD.Print($"HOST_CAPTURED {_capture} {error} present={_liveSession.PresentCount}");
                if (error != Error.Ok)
                {
                    exitCode = 1;
                }
            }

            FinishLiveLog();
            GetTree().Quit(exitCode);
        }
    }

    private void AdvanceLiveCore()
    {
        LevelSession session = _liveSession!;
        long before = session.PresentCount;
        try
        {
            session.AdvanceToNextPresent();
            if (session.PresentCount != before)
            {
                AccumulateCloudDrift(session);
            }
        }
        catch (NotSupportedException e)
        {
            _liveFailure = true;
            _liveStop = $"Preview stopped at an unsupported behavior.\n{e.Message}";
            GD.PrintErr($"HOST_LIVE_UNSUPPORTED present={session.PresentCount}: {e}");
        }
    }

    private void FailLive(Exception error)
    {
        _liveFailure = true;
        _liveStop = "The preview stopped after an error. See the host log for details.";
        GD.PrintErr($"HOST_LIVE_ERROR present={_liveSession?.PresentCount}: {error}");
        _liveInput?.SetFocused(false);
        ReleaseLiveMouse();
        UpdateLiveNotice();
    }

    private void CheckLiveEnd()
    {
        if (_liveStop is null && _liveSession is { } session)
        {
            if (session.Ended)
            {
                _liveStop = $"Level ended: {session.Game.Quit}.\nReturn to the front end is not available in this preview.";
            }
            else if (_liveEndPresent is { } end && session.PresentCount >= end)
            {
                _liveStop = "Preview complete.";
            }
        }

        if (_liveStop is not null)
        {
            _liveInput?.SetFocused(false);
            _liveAccumulator = 0;
        }
    }

    private void UpdateLiveNotice()
    {
        if (_liveSession is not { } session || _liveNotice is null || _liveBanner is null)
        {
            return;
        }

        _liveBanner.Text = _liveReplay ? "Level 100 replay · silent preview" : session.Game.State == GameState.Panning
            ? "Level 100 · silent preview · Enter skips intro" : "Level 100 · silent preview · Esc pauses";
        _liveNotice.Text = _liveStop ?? (!_liveReplay && !_liveFocused
            ? "Preview suspended while this window is unfocused."
            : session.Game.Paused ? (_liveReplay ? "Recorded pause — following the tape.\nPhysical input is disabled during replay."
                : "Paused — Esc resumes.\nOther pause-menu options are not available in this preview.") : "");
        _liveNotice.Visible = _liveNotice.Text.Length != 0;
        bool capture = !_liveReplay && _liveFocused && _liveStop is null && !session.Game.Paused;
        if (capture && !_liveOwnsMouse)
        {
            _livePreviousMouseMode = Input.MouseMode;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            _liveOwnsMouse = true;
        }
        else if (!capture)
        {
            ReleaseLiveMouse();
        }
    }

    private void ReleaseLiveMouse()
    {
        if (_liveOwnsMouse)
        {
            Input.MouseMode = _livePreviousMouseMode;
            _liveOwnsMouse = false;
        }
    }

    private void UpdateLivePresentation()
    {
        LevelSession session = _liveSession!;
        if (!_liveDraw || _liveCamera is null || session.Game.GetCurrentCamera(0) is not { } camera)
        {
            return;
        }

        (FVector pos, FMatrix ori, float zoom) = Interpolated(camera, session.Game.Loop.Fraction2);
        Place(_liveCamera, pos, ori, zoom);
        if (_liveSky is not null)
        {
            _liveSky.Position = ToGodot(pos);
        }

        _liveTerrain!.Update(_liveTerrain.ChooseLevels(_landscapeView), new Vector2(_cloudU, _cloudV));
        _liveMeshes!.Update(session);
        _liveWater?.Update(pos, ori, _waterAngle);
        _liveSurf?.Update(pos, ori, _waterAngle, _waterScroll);
        _liveHud?.Dispose();
        _liveHud = null;
        _liveBattleLine?.Dispose();
        _liveBattleLine = null;
        _liveParticles?.Dispose();
        _liveParticles = null;

        var engine = session.Game.GetPlayer(0)?.BattleEngine;
        bool showHud = session.Game.GetPlayer(0) is not null && camera.GetShowHud();
        if (session.PresentCount > 3 && showHud && engine is BattleEngine be && !be.IsDying)
        {
            _liveHud = new TransientHostView(HudView.Build(be, _liveTextures!, session.Context.World.ObjectiveThings.ToList(),
                ref _lastHealth, ref _lastEnergy, _dial ??= _liveFiles!.Read(@"data\Dial.raw").Bytes,
                session.Game.Loop.Fraction, session.Context.World.UnitNB.ToList(), _ammo,
                session.Context.Particles?.RenderFrameLength ?? 1.0f, _liveHudTextures));
            _view!.AddChild(_liveHud.Node);
        }

        if (showHud && session.Context.InfluenceMap is { } influence)
        {
            var nodes = influence.Nodes.ToList();
            _battleLine ??= BattleLineData.Build(session.Context.Map, nodes);
            _liveBattleLine = new TransientHostView(BattleLineView.Build(_battleLine, nodes, engine as BattleEngine,
                session.Context.World.ObjectiveThings.ToList(), session.Context.World.SquadNB.ToList(),
                session.Context.Events.Time, _liveTextures!), ownsShaders: true);
            _view!.AddChild(_liveBattleLine.Node);
        }

        if (session.Context.Particles is { } particles)
        {
            _liveParticles = new TransientHostView(ParticleView.Build(particles.Manager, _liveTextures!, pos, ori, session.Context.Events.Time));
            _liveWorld!.AddChild(_liveParticles.Node);
        }
    }

    private void FinishLiveLog()
    {
        if (!_liveFinished && _liveSession is { } session)
        {
            _liveFinished = true;
            GD.Print($"HOST_LIVE_DONE present={session.PresentCount} time={session.Context.Events.Time:R} state={session.Game.State} quit={session.Game.Quit} " +
                $"ended={session.Ended} failure={_liveFailure} things={_liveMeshes?.ThingCount} parts={_liveMeshes?.PartCount} " +
                $"discarded_wall_seconds={_liveDiscardedSeconds:R}");
        }
    }

    public override void _ExitTree()
    {
        if (!_liveMode || _liveDisposed)
        {
            return;
        }

        _liveDisposed = true;
        FinishLiveLog();
        if (_liveWindow is not null && GodotObject.IsInstanceValid(_liveWindow))
        {
            _liveWindow.FocusEntered -= LiveFocusEntered;
            _liveWindow.FocusExited -= LiveFocusExited;
        }

        _liveInput?.SetFocused(false);
        ReleaseLiveMouse();
        _liveHud?.Dispose();
        _liveBattleLine?.Dispose();
        _liveParticles?.Dispose();
        _liveHudTextures?.Dispose();
        _liveMeshes?.Dispose();
        _liveWater?.Dispose();
        _liveSurf?.Dispose();
        _liveTerrain?.Dispose();
        _battleLine?.Height.Dispose();
        _battleLine?.Influence.Dispose();
        _liveTrace?.Dispose();
        _liveSession?.Context.Particles?.Manager.Shutdown();
        _liveSession = null;
        _liveTextures = null;
        _liveFiles = null;
    }
}
