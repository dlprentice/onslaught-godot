// SPDX-License-Identifier: GPL-3.0-only
using Godot;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The Host's one shutdown routine. Every way the Host leaves goes through ReleaseHost while the engine and its servers
/// are still running: its own quits (QuitHost), the window's close request and, for any exit not routed through either,
/// leaving the tree as the engine finalizes it. A C# wrapper of an engine object that is finalized after the engine has
/// shut down calls into it and crashes the process at exit; on Godot 4.8 beta4 that was seen as a SIGSEGV on a runtime
/// thread in managed code while the main thread was already in exit(). So the Host drops every engine object it holds,
/// then runs the collector and the pending finalizers before the engine goes. Each release step is isolated: a step that
/// throws is logged, the steps after it still run, the drain always runs, the quit always happens, and an exit that
/// would have returned 0 returns 1.
/// </summary>
public partial class Host
{
    private const int MaxDrainPasses = 8;

    private bool _hostReleased;
    private int _releaseFailures;

    /// <summary>Quits the Host with this exit code (1 instead of 0 if a release step failed) after releasing what it built.</summary>
    private void QuitHost(int exitCode)
    {
        try
        {
            ExitStress();
            ReleaseHost(freeNodes: true);
        }
        catch (Exception e)
        {
            ReleaseFailed("quit", e);
        }
        finally
        {
            GetTree().Quit(ExitCodeAfterRelease(exitCode));
        }
    }

    public override void _Notification(int what)
    {
        // The window's close button: release now, while the main loop runs; the tree then quits (auto-accept quit).
        if (what == NotificationWMCloseRequest)
        {
            try
            {
                ReleaseHost(freeNodes: true);
            }
            catch (Exception e)
            {
                ReleaseFailed("close request", e);
            }
            finally
            {
                if (_releaseFailures > 0)
                {
                    GetTree().Quit(1);
                }
            }
        }
    }

    public override void _ExitTree()
    {
        try
        {
            ReleaseHost(freeNodes: false);
        }
        catch (Exception e)
        {
            ReleaseFailed("exit tree", e);
        }
        finally
        {
            if (_releaseFailures > 0 && IsInsideTree())
            {
                GetTree().Quit(1);
            }
        }
    }

    private int ExitCodeAfterRelease(int exitCode)
    {
        if (_releaseFailures == 0 || exitCode != 0)
        {
            return exitCode;
        }

        GD.PrintErr($"HOST_SHUTDOWN_FAILED: {_releaseFailures} release step(s) failed; the Host exits with code 1");
        return 1;
    }

    /// <summary>
    /// Ends the mode's run, disposes and drops the views, nodes and resources the Host holds, clears the views' shared
    /// shaders and drains finalizers. Child nodes are freed here only from the main loop (a quit or the close request);
    /// when the tree itself is finalizing it frees them. The drain runs on every call, so a later call still collects
    /// what the engine released in between.
    /// </summary>
    private void ReleaseHost(bool freeNodes)
    {
        if (!_hostReleased)
        {
            _hostReleased = true;
            ReleaseStep("mode", ReleaseMode);
            DisposeViews();
            if (freeNodes)
            {
                foreach (Node child in GetChildren())
                {
                    ReleaseStep($"node {child.Name}", () =>
                    {
                        RemoveChild(child);
                        child.Free();
                    });
                }
            }

            ReleaseStep("front-end view", () => _flowView?.Release());
            ReleaseStep("front-end overlay", () => _flowOverlay?.Release());
            ReleaseStep("level textures", () => _flowLevelTextures?.Release());
            ReleaseStep("particle textures", () => _flowParticleTextures?.Release());
            ReleaseStep("live textures", () => _liveTextures?.Release());
            ReleaseStep("single-Present textures", () => _singleTextures?.Release());
            _singleTextures = null;
            _view = null;
            _liveWorld = null;
            _liveCamera = null;
            _liveSky = null;
            _liveImage = null;
            _liveBanner = null;
            _liveNotice = null;
            _liveWindow = null;
            _flowInputWindow = null;
            _flowStatus = null;
            _flowView = null;
            _flowOverlay = null;
            _flowRaster = null;
            _flowFrame = null;
            _flowLevelFrame = null;
            _flowOverlayFrame = null;
            _flowLevelSurf = null;
            _flowLevelTextures = null;
            _flowParticleTextures = null;
            _flowFiles = null;
            _liveTextures = null;
            _liveFiles = null;
            _liveSession = null;
            ReleaseStep("water shader", WaterView.ReleaseShared);
            ReleaseStep("particle shaders", ParticleView.ReleaseShared);
        }

        DrainFinalizers();
    }

    /// <summary>
    /// Collects and runs pending finalizers until two passes in a row release no engine object: the engine's live
    /// object count (Performance ObjectCount) after each is not below the count before it. One quiet pass can follow
    /// a finalizer that only made more garbage (a managed owner whose finalizer drops a wrapper), which the next pass
    /// collects; two in a row also keeps the earlier minimum of two passes. At most MaxDrainPasses passes.
    /// </summary>
    private static void DrainFinalizers()
    {
        double before = Performance.GetMonitor(Performance.Monitor.ObjectCount);
        int quiet = 0;
        for (int pass = 1; pass <= MaxDrainPasses; pass++)
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            double after = Performance.GetMonitor(Performance.Monitor.ObjectCount);
            quiet = after >= before ? quiet + 1 : 0;
            if (quiet == 2)
            {
                return;
            }

            before = after;
        }
    }

    private void ReleaseStep(string step, Action release)
    {
        try
        {
            release();
        }
        catch (Exception e)
        {
            ReleaseFailed(step, e);
        }
    }

    private void ReleaseFailed(string step, Exception e)
    {
        _releaseFailures++;
        GD.PrintErr($"HOST_RELEASE_FAILED {step}: {e.GetType().Name}: {e.Message.ReplaceLineEndings(" ")}");
    }

    /// <summary>Disposes and drops the level and HUD views, each in its own step (each view's Dispose is idempotent).</summary>
    private void DisposeViews()
    {
        ReleaseStep("hud", () => _liveHud?.Dispose());
        ReleaseStep("battle line", () => _liveBattleLine?.Dispose());
        ReleaseStep("live particles", () => _liveParticles?.Dispose());
        ReleaseStep("hud textures", () => _liveHudTextures?.Dispose());
        ReleaseStep("meshes", () => _liveMeshes?.Dispose());
        ReleaseStep("target box", () => _targetBox?.Dispose());
        ReleaseStep("water", () => _liveWater?.Dispose());
        ReleaseStep("surf", () => _liveSurf?.Dispose());
        ReleaseStep("terrain", () => _liveTerrain?.Dispose());
        ReleaseStep("battle line height", () => _battleLine?.Height.Dispose());
        ReleaseStep("battle line influence", () => _battleLine?.Influence.Dispose());
        ReleaseStep("flow particles", () => _flowParticles?.Dispose());
        _liveHud = _liveBattleLine = _liveParticles = _flowParticles = null;
        _liveHudTextures = null;
        _liveMeshes = null;
        _targetBox = null;
        _liveWater = null;
        _liveSurf = null;
        _liveTerrain = null;
        _battleLine = null;
    }
}
