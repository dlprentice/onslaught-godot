// SPDX-License-Identifier: GPL-3.0-only
using Godot;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The Host's one shutdown routine. Every way the Host leaves goes through ReleaseHost while the engine and its servers
/// are still running: its own quits (QuitHost), the window's close request and, for any exit not routed through either,
/// leaving the tree as the engine finalizes it. A C# wrapper of an engine object that is finalized after the engine has
/// shut down calls into it and crashes the process at exit; on Godot 4.8 beta4 that was seen as a SIGSEGV on a runtime
/// thread in managed code while the main thread was already in exit(). So the Host drops every engine object it holds,
/// then runs the collector and the pending finalizers before the engine goes.
/// </summary>
public partial class Host
{
    private bool _hostReleased;

    /// <summary>Quits the Host with this exit code after releasing what it built.</summary>
    private void QuitHost(int exitCode)
    {
        ReleaseHost(freeNodes: true);
        GetTree().Quit(exitCode);
    }

    public override void _Notification(int what)
    {
        // The window's close button: release now, while the main loop runs; the tree then quits (auto-accept quit).
        if (what == NotificationWMCloseRequest)
        {
            ReleaseHost(freeNodes: true);
        }
    }

    public override void _ExitTree() => ReleaseHost(freeNodes: false);

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
            ReleaseMode();
            DisposeViews();
            if (freeNodes)
            {
                foreach (Node child in GetChildren())
                {
                    RemoveChild(child);
                    child.Free();
                }
            }

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
            WaterView.ReleaseShared();
            ParticleView.ReleaseShared();
        }

        for (int pass = 0; pass < 2; pass++)
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }
    }

    /// <summary>Disposes and drops the level and HUD views (each view's Dispose is idempotent).</summary>
    private void DisposeViews()
    {
        _liveHud?.Dispose();
        _liveBattleLine?.Dispose();
        _liveParticles?.Dispose();
        _liveHudTextures?.Dispose();
        _liveMeshes?.Dispose();
        _targetBox?.Dispose();
        _liveWater?.Dispose();
        _liveSurf?.Dispose();
        _liveTerrain?.Dispose();
        _battleLine?.Height.Dispose();
        _battleLine?.Influence.Dispose();
        _flowParticles?.Dispose();
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
