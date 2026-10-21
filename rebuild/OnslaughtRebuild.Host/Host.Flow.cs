// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using Godot;
using OnslaughtRebuild.Parity;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Game;

namespace OnslaughtRebuild.Host;

/// <summary>
/// HOST_LIVE=flow: the whole game, boot to quit (<see cref="GameRun"/>), on its own thread through
/// <see cref="SteppedGameRun"/>. Godot's thread advances it one Present per fixed step and only ever reads the immutable
/// <see cref="FlowSnapshot"/> each Present hands back; keys go to the run's thread through its input queue. HOST_TAPE
/// replays a tape instead of the keyboard; the installation's savegames are on the card (HOST_INSTALL_SAVES=0 leaves
/// them off) and HOST_SAVES ("NAME=FILE;...") adds career saves;
/// HOST_FULL_BOOT=1 plays the boot films (default: as -skipfmv); HOST_TRACE writes each snapshot's line; HOST_END_PRESENT
/// stops the run; HOST_DRAW=0 (with a tape) runs as fast as it can. HOST_FLOW_STRAIGHT=1 (with a tape, an end and a trace)
/// instead runs the same game straight through on this thread, writing the reference the stepped trace must equal.
/// The front end is drawn by Godot (<see cref="FrontEndView"/>) from the frames its render path records; the level is not
/// drawn yet. HOST_SHOTS ("P,P,...") with HOST_SHOT_DIR saves those Presents' pictures: always the software reference
/// (<see cref="FrontEndRaster"/>, shot-NNNNN-raster.png), and with HOST_SHOT_GODOT=1 (a GPU run) Godot's
/// (shot-NNNNN-godot.png).
/// </summary>
public partial class Host
{
    private bool _flowMode;
    private bool _flowDraw;
    private bool _flowReplay;
    private SteppedGameRun? _flowRun;
    private StreamWriter? _flowTrace;
    private FlowSnapshot? _flowLast;
    private long? _flowEnd;
    private double _flowAccumulator;
    private string? _flowStop;
    private bool _flowFailure;
    private int _flowSettle;
    private Label? _flowStatus;
    private CanvasFrame? _flowFrame;
    private FrontEndView? _flowView;
    private FrontEndRaster? _flowRaster;
    private HashSet<long>? _flowShots;
    private string? _flowShotDir;
    private bool _flowShotGodot;
    private long? _flowPendingShot;
    private int _flowShotWait;
    private GameFiles? _flowFiles;
    private TextureLibrary? _flowLevelTextures;
    private SurfView? _flowLevelSurf;
    private bool _flowLevelShown;
    private CanvasFrame? _flowLevelFrame;
    private FrontEndView? _flowOverlay;
    private CanvasFrame? _flowOverlayFrame;

    private void ReadyFlow()
    {
        _flowMode = true;
        _flowDraw = System.Environment.GetEnvironmentVariable("HOST_DRAW") != "0";
        string? tape = System.Environment.GetEnvironmentVariable("HOST_TAPE");
        _flowReplay = tape is not null;
        if (!_flowDraw && !_flowReplay)
        {
            throw new ArgumentException("HOST_DRAW=0 requires HOST_TAPE; physical input requires the visible preview");
        }

        if (System.Environment.GetEnvironmentVariable("HOST_END_PRESENT") is { } endText)
        {
            _flowEnd = long.Parse(endText);
        }

        GameFiles files = GameFiles.Find() ?? throw new InvalidOperationException("the game installation was not found (set ONSLAUGHT_GAME_DIR)");
        bool fullBoot = System.Environment.GetEnvironmentVariable("HOST_FULL_BOOT") == "1";
        var run = new GameRun(files.Read, id => new RetailPcText(files.Read("BEA.exe").Bytes, 0)[id],
            new InstalledFilmHeaders(files.Read), !fullBoot, LevelSession.VirtualVoiceFor(files.Read));
        if (tape is not null)
        {
            run.Tape = RetailTape.Load(tape);
        }

        // The installation's own saves are on the card, as the game run from its folder (and the port, whose path layer
        // reads the installation beneath its user folder) sees them; HOST_INSTALL_SAVES=0 leaves the card empty.
        string installSaves = Path.Combine(files.Root, "savegames");
        if (System.Environment.GetEnvironmentVariable("HOST_INSTALL_SAVES") != "0" && Directory.Exists(installSaves))
        {
            foreach (string file in Directory.GetFiles(installSaves).Where(f => f.EndsWith(".bes", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
            {
                run.FrontEnd.MemoryCard.Folder.Write(Path.GetFileNameWithoutExtension(file), File.ReadAllBytes(file));
            }
        }

        if (System.Environment.GetEnvironmentVariable("HOST_SAVES") is { } saves)
        {
            foreach (string entry in saves.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = entry.Split('=', 2);
                run.FrontEnd.MemoryCard.Folder.Write(pair[0], File.ReadAllBytes(pair[1]));
            }
        }

        if (System.Environment.GetEnvironmentVariable("HOST_TRACE") is { } trace)
        {
            _flowTrace = new StreamWriter(File.Open(trace, FileMode.CreateNew));
        }

        if (System.Environment.GetEnvironmentVariable("HOST_FLOW_STRAIGHT") == "1")
        {
            RunFlowStraight(run);
            return;
        }

        if (System.Environment.GetEnvironmentVariable("HOST_SHOTS") is { } shots)
        {
            _flowShots = [.. shots.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(long.Parse)];
            _flowShotDir = System.Environment.GetEnvironmentVariable("HOST_SHOT_DIR")
                ?? throw new ArgumentException("HOST_SHOTS requires HOST_SHOT_DIR");
            Directory.CreateDirectory(_flowShotDir);
            _flowShotGodot = System.Environment.GetEnvironmentVariable("HOST_SHOT_GODOT") == "1";
        }

        if (_flowDraw || _flowShots is not null)
        {
            // The run's thread records each front-end frame; the Step that returns its Present hands it over.
            var images = new FrontEndImages(files.Read);
            var feTextures = new FrontEndTextures(files.Read);
            run.FrontEnd.Draw = new FrontEndDraw(feTextures)
            {
                Canvas = new RecordingCanvas(frame => Volatile.Write(ref _flowFrame, frame)),
            };
            // Each level's 2D overlays (pause and end menus, the end screen), drawn over its picture.
            run.LevelDraw = () => new FrontEndDraw(feTextures)
            {
                Canvas = new RecordingCanvas(frame => Volatile.Write(ref _flowLevelFrame, frame)),
            };
            _flowRaster = new FrontEndRaster(images.Get);
            if (_flowDraw || _flowShotGodot)
            {
                // Each level's textures and surf, read from its resource file as it loads (on the run's thread; the views
                // are built from them on this thread while the run is parked).
                _flowFiles = files;
                run.LevelChunks = _ =>
                {
                    var textures = new TextureLibrary(files.Read);
                    var surf = new SurfView();
                    _flowLevelTextures = textures;
                    _flowLevelSurf = surf;
                    return (tag, c) => textures.ReadChunk(tag, c) || surf.ReadChunk(tag, c);
                };
                _flowView = new FrontEndView(images);
                AddChild(_flowView.Viewport);
                _flowOverlay = new FrontEndView(images, overlay: true);
                AddChild(_flowOverlay.Viewport);
                AddChild(new TextureRect
                {
                    Name = "FrontEndImage",
                    Texture = _flowView.Viewport.GetTexture(),
                    Size = new Vector2(FrontEndRaster.Width, FrontEndRaster.Height),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                });
            }
        }

        _flowRun = new SteppedGameRun(run);
        _flowStatus = new Label { Name = "FlowStatus", Position = new Vector2(8, 6), MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_flowStatus);
        GD.Print($"HOST_FLOW_READY replay={_flowReplay} drawing={_flowDraw} full_boot={fullBoot} silent=true");
    }

    private void ProcessFlow(double delta)
    {
        if (_flowRun is null)
        {
            return;
        }

        if (_flowPendingShot is { } pending)
        {
            // Godot's picture of the shot's frame is ready once it has been drawn.
            if (++_flowShotWait >= 3)
            {
                SaveGodotShot(pending);
                _flowPendingShot = null;
            }

            return;
        }

        if (_flowStop is null)
        {
            if (_flowShots is not null)
            {
                // One Present a frame, so that each shot is taken of its own frame.
                StepFlow();
            }
            else if (!_flowDraw)
            {
                // A replay without drawing: as many Presents as a frame allows; time is the run's own.
                for (int i = 0; i < 2000 && _flowStop is null; i++)
                {
                    StepFlow();
                }
            }
            else
            {
                _flowAccumulator += Math.Clamp(delta, 0, PlatformClock.ReplayDt * 4);
                int steps = 0;
                while (_flowAccumulator >= PlatformClock.ReplayDt && steps++ < 4 && _flowStop is null)
                {
                    _flowAccumulator -= PlatformClock.ReplayDt;
                    StepFlow();
                }
            }

            UpdateFlowStatus();
        }
        else if (++_flowSettle == 4)
        {
            FinishFlow();
            GetTree().Quit(_flowFailure ? 1 : 0);
        }
    }

    private void StepFlow()
    {
        FlowSnapshot? snapshot = _flowRun!.Step();
        if (snapshot is null)
        {
            _flowFailure = _flowRun.Failure is not null;
            _flowStop = _flowRun.Failure is { } failure
                ? $"The run stopped at an unsupported behavior or error after Present {_flowLast?.Present}.\n{failure.Message}"
                : $"The game quit (exit code {_flowRun.ExitCode}).";
            if (_flowRun.Failure is { } e)
            {
                GD.PrintErr($"HOST_FLOW_STOPPED present={_flowLast?.Present}: {e}");
            }

            return;
        }

        _flowLast = snapshot;
        _flowTrace?.WriteLine(snapshot.Line());
        if (_flowFiles is not null)
        {
            ShowFlowLevel(snapshot);
        }

        if (Interlocked.Exchange(ref _flowFrame, null) is { } frame && frame.Present == snapshot.Present)
        {
            _flowView?.Show(frame);
            if (_flowShots?.Contains(snapshot.Present) == true)
            {
                _flowRaster!.Draw(frame);
                SaveRasterShot(snapshot.Present);
                if (_flowShotGodot)
                {
                    _flowPendingShot = snapshot.Present;
                    _flowShotWait = 0;
                }
            }
        }
        else if (_flowLevelShown && _flowShotGodot && _flowShots?.Contains(snapshot.Present) == true)
        {
            // The level is drawn by Godot only.
            _flowPendingShot = snapshot.Present;
            _flowShotWait = 0;
        }

        if (_flowEnd is { } end && snapshot.Present >= end)
        {
            _flowStop = "Preview complete.";
        }
    }

    private void UpdateFlowStatus()
    {
        if (_flowStatus is null || _flowLast is not { } s)
        {
            return;
        }

        string where = s.Segment switch
        {
            FlowSegment.FrontEnd => $"front end, page {(FrontEndPage)BinaryPrimitives.ReadInt32LittleEndian(Convert.FromHexString(s.FrontEndLine![8..16]))}",
            FlowSegment.Level => $"level {BinaryPrimitives.ReadInt32LittleEndian(Convert.FromHexString(s.LevelFields!["LVL"]))}",
            _ => "boot",
        };
        _flowStatus.Text = $"Present {s.Present} · {where} · {(_flowReplay ? "replay" : "keyboard")} · silent preview"
            + (_flowStop is null ? "" : $"\n{_flowStop}");
    }

    private void InputFlow(InputEvent input)
    {
        if (_flowReplay || _flowRun is null || _flowStop is not null || input is not InputEventKey key || key.Echo)
        {
            return;
        }

        if (HostKeyMap.TryMap(key.PhysicalKeycode, key.Location, out HostKeyMap.PcKey pc))
        {
            bool down = key.Pressed;
            _flowRun.Post(run => run.Input.PostTapeKey(pc.VirtualKey, pc.ScanCode, down));
            GetViewport().SetInputAsHandled();
        }
    }

    private void SaveRasterShot(long present, string kind = "raster")
    {
        uint[] pixels = _flowRaster!.Pixels;
        var rgb = new byte[pixels.Length * 3];
        for (int i = 0; i < pixels.Length; i++)
        {
            rgb[i * 3] = (byte)(pixels[i] >> 16);
            rgb[i * 3 + 1] = (byte)(pixels[i] >> 8);
            rgb[i * 3 + 2] = (byte)pixels[i];
        }

        string path = Path.Combine(_flowShotDir!, $"shot-{present:00000}-{kind}.png");
        Error error = Image.CreateFromData(FrontEndRaster.Width, FrontEndRaster.Height, false, Image.Format.Rgb8, rgb).SavePng(path);
        GD.Print($"HOST_SHOT {path} {error}");
    }

    private void SaveGodotShot(long present)
    {
        using Image image = (_flowLevelShown ? _view! : _flowView!.Viewport).GetTexture().GetImage();
        if (_flowLevelShown && _flowOverlay is not null)
        {
            // The level's picture with its overlay over it, as the window shows them.
            using Image over = _flowOverlay.Viewport.GetTexture().GetImage();
            image.Convert(Image.Format.Rgba8);
            over.Convert(Image.Format.Rgba8);
            if (_flowOverlayFrame is { } overlayFrame && overlayFrame.Present == present)
            {
                // The same overlay by the software rasterizer over Godot's level picture, which separates the
                // overlay's drawing from the level's when compared with the port.
                byte[] level = image.GetData();
                var picture = new uint[level.Length / 4];
                for (int i = 0; i < picture.Length; i++)
                {
                    picture[i] = 0xff000000u | ((uint)level[i * 4] << 16) | ((uint)level[i * 4 + 1] << 8) | level[i * 4 + 2];
                }

                _flowRaster!.DrawOver(picture, overlayFrame);
                SaveRasterShot(present, "levelraster");
                if (System.Environment.GetEnvironmentVariable("HOST_SHOT_LAYERS") == "1")
                {
                    // The overlay alone: Godot's (premultiplied RGBA) and the rasterizer's over black.
                    over.SavePng(Path.Combine(_flowShotDir!, $"shot-{present:00000}-overlay-godot.png"));
                    _flowRaster.DrawOver(new uint[picture.Length], overlayFrame);
                    SaveRasterShot(present, "overlay-raster");
                }
            }

            // The overlay's colours are premultiplied (its quads blend over a transparent black clear), so it goes over
            // as premultiplied alpha, as the window's TextureRect draws it.
            byte[] dst = image.GetData(), src = over.GetData();
            for (int i = 0; i < dst.Length; i += 4)
            {
                int keep = 255 - src[i + 3];
                for (int c = 0; c < 3; c++)
                {
                    dst[i + c] = (byte)Math.Min(255, src[i + c] + (dst[i + c] * keep + 127) / 255);
                }
            }

            image.SetData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, dst);
        }
        string path = Path.Combine(_flowShotDir!, $"shot-{present:00000}-godot.png");
        Error error = image.SavePng(path);
        GD.Print($"HOST_SHOT {path} {error}");
    }

    /// <summary>
    /// The level in the flow: while a level runs, its views (those of the live level host) are built when it starts and
    /// updated each Present from the run's objects, read through <see cref="SteppedGameRun.Inspect"/> while the run is
    /// parked; the front end's picture is hidden. When the level ends they are freed and the front end shows again.
    /// </summary>
    private void ShowFlowLevel(FlowSnapshot snapshot)
    {
        bool inLevel = snapshot.Segment == FlowSegment.Level;
        if (inLevel && !_flowLevelShown)
        {
            _flowRun!.Inspect(run =>
            {
                _liveSession = run.Level ?? throw new InvalidOperationException("a level segment without a level session");
                _liveTextures = _flowLevelTextures ?? throw new InvalidOperationException("the level's textures were not read");
                _liveFiles = _flowFiles;
                _liveDraw = true;
                BuildLiveScene(_flowLevelSurf!);
            });
            _flowLevelShown = true;
            SetFrontEndVisible(false);
            if (_flowOverlay is not null)
            {
                // The overlay's picture over the level's (added after the level's image, so drawn above it).
                AddChild(new TextureRect
                {
                    Name = "LevelOverlayImage",
                    Texture = _flowOverlay.Viewport.GetTexture(),
                    Size = new Vector2(FrontEndRaster.Width, FrontEndRaster.Height),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    // Its colours are premultiplied: its quads blend over a transparent black clear.
                    Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha },
                });
            }
        }
        else if (!inLevel && _flowLevelShown)
        {
            FreeFlowLevel();
            SetFrontEndVisible(true);
            return;
        }

        if (_flowLevelShown)
        {
            // As the live host does after each Present: the frame length's cloud and water drift and the smoothed
            // landscape view that chooses the terrain's texture levels, then the views.
            _flowRun!.Inspect(_ =>
            {
                AccumulateCloudDrift(_liveSession!);
                UpdateLivePresentation();
            });
            if (Interlocked.Exchange(ref _flowLevelFrame, null) is { } overlay && overlay.Present == snapshot.Present)
            {
                _flowOverlay?.Show(overlay);
                _flowOverlayFrame = overlay;
            }
        }
    }

    private void SetFrontEndVisible(bool visible)
    {
        if (GetNodeOrNull<TextureRect>("FrontEndImage") is { } image)
        {
            image.Visible = visible;
        }
    }

    /// <summary>Frees the level's views and nodes (as the live host's exit does), leaving the run's session to the run.</summary>
    private void FreeFlowLevel()
    {
        _liveHud?.Dispose();
        _liveBattleLine?.Dispose();
        _liveParticles?.Dispose();
        _liveHudTextures?.Dispose();
        _liveMeshes?.Dispose();
        _targetBox?.Dispose();
        _targetBox = null;
        _liveWater?.Dispose();
        _liveSurf?.Dispose();
        _liveTerrain?.Dispose();
        _battleLine?.Height.Dispose();
        _battleLine?.Influence.Dispose();
        _liveHud = _liveBattleLine = _liveParticles = null;
        _liveHudTextures = null;
        _liveMeshes = null;
        _liveWater = null;
        _liveSurf = null;
        _liveTerrain = null;
        _battleLine = null;
        _liveImage?.QueueFree();
        _liveImage = null;
        GetNodeOrNull("LevelOverlayImage")?.Free();
        _flowOverlay?.Show(new CanvasFrame(0, null, []));
        _view?.QueueFree();
        _view = null;
        _liveWorld = null;
        _liveCamera = null;
        _liveSky = null;
        _liveSession = null;
        _liveTextures = null;
        _flowLevelShown = false;
    }

    private sealed class StopStraight : Exception;

    /// <summary>The reference: the run straight through on Godot's thread, each Present's line written as it ends.</summary>
    private void RunFlowStraight(GameRun run)
    {
        if (_flowTrace is null || _flowEnd is not { } end || !_flowReplay)
        {
            throw new ArgumentException("HOST_FLOW_STRAIGHT requires HOST_TAPE, HOST_END_PRESENT and HOST_TRACE");
        }

        run.OnPresented = () =>
        {
            _flowTrace.WriteLine(FlowSnapshot.Capture(run).Line());
            if (run.Clock.PresentCount >= end)
            {
                throw new StopStraight();
            }
        };
        string result;
        try
        {
            result = $"exit {run.Run()}";
        }
        catch (StopStraight)
        {
            result = "end";
        }

        _flowTrace.Dispose();
        _flowTrace = null;
        GD.Print($"HOST_FLOW_STRAIGHT_FINISHED present={run.Clock.PresentCount} result={result}");
        GetTree().Quit(0);
    }

    private void FinishFlow()
    {
        _flowTrace?.Dispose();
        _flowTrace = null;
        _flowRun?.Dispose();
        _flowRun = null;
        GD.Print($"HOST_FLOW_FINISHED present={_flowLast?.Present} failure={_flowFailure} stop={_flowStop?.Split('\n')[0]}");
    }
}
