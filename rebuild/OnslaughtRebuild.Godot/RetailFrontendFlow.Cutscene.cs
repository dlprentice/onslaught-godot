// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;
using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// Retail's level intro cutscene — the single largest piece of authored
/// narrative on the startup-through-Level-100 path, and until now the one this
/// reconstruction cut straight through.
///
/// <para><b>Where it sits, and why here.</b></para>
/// <c>CGame::RunIntroFMV</c> (<c>references/Onslaught/game.cpp:1122-1152</c>) is
/// called from exactly one place, <c>CGame::RestartLoopRunLevel</c> at
/// <c>game.cpp:1336-1345</c>:
///
/// <code>
/// if (GetIntroFMV()!=-1)
/// {
///     CONSOLE.SetLoadingFraction(1.f);
///     CONSOLE.SetLoading(FALSE);
///     RunIntroFMV();
///     CONSOLE.SetLoading(TRUE);
///     ...
/// }
/// </code>
///
/// So the movie plays AFTER the level is loaded and BEFORE the first gameplay
/// frame, with the loading screen driven to 100 % and then dismissed. This lane
/// therefore hangs it off the existing loading seam: the world is already built
/// and <c>MarkLevel100Ready</c> has fired before a single frame of video is
/// drawn, which is both what retail does and what keeps the movie from
/// competing with world construction for the disk.
///
/// <para><b>Which clip, from bytes.</b></para>
/// The campaign FMV table lives at VA <c>0x0063FD70</c>, file offset
/// <c>0x23FD70</c>, in the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, sha256
/// <c>74154BFAE14DDC8ECB87A0766F5BC381C7B7F1AB334ED7A753040EDA1E1E7750</c>. Its
/// records are <c>[i32 level][i32 intro][i32 outroA][i32 outroB]</c> and row 0
/// reads <c>100, 1, 2, -1</c>. <c>CGame::RunIntroFMV</c> formats that id with
/// <c>sprintf(foo, "cutscenes\\%02d", fmv)</c> (<c>game.cpp:1130</c>), giving
/// <c>data/video/cutscenes/01.vid</c> — 480x300 at 25 fps, 3,095 frames,
/// 123.80 s, 32,067,000 bytes, confirmed by <c>ffprobe</c> against the safe
/// copy.
///
/// <para><b>The outro is deliberately NOT here.</b></para>
/// The same table row names <c>02.vid</c> as Level 100's outro, and
/// <c>CGame::RunOutroFMV</c> (<c>game.cpp:1156-1233</c>) would play it — but only
/// under <c>(mQuit == QT_QUIT_TO_FRONTEND) &amp;&amp; (END_LEVEL_DATA.mFinalState ==
/// GAME_STATE_LEVEL_WON)</c> (<c>game.cpp:1166</c>). This frontend does not
/// inspect mission terminal state and does not own a result lifecycle — that
/// boundary is stated in <c>rebuild/PROVENANCE.md</c> — so the only signal it
/// has at the teardown seam is "the player left the level", which is true of a
/// loss and of a quit as much as of a win. Playing the victory cutscene on that
/// signal would be a fabrication, so nothing is played and no dead state is
/// added. See <c>local-lab/FMV-IMPLEMENTED-2026-07-28.md</c>.
/// </summary>
public sealed partial class RetailFrontendFlow
{
    private RetailStartupSequence? _introCutscene;

    /// <summary>
    /// Starts the level intro cutscene if retail would have played one and the
    /// clip is actually decoded. Returns false when the flow should fall through
    /// to gameplay directly, which is the pre-existing behaviour.
    ///
    /// <para>Nothing is imitated. A missing decode produces an absent cutscene
    /// and a warning, never a stand-in, a black hold, or a shortened stub.</para>
    /// </summary>
    private bool TryBeginLevel100IntroCutscene()
    {
        if (!_session.Level100IntroCutscenePending)
        {
            // mFirstTimeRound is FALSE: this is a Retry inside the same restart
            // loop, and retail does not replay the movie.
            return false;
        }

        string[] arguments = OS.GetCmdlineUserArgs();
        if (RetailStartupSchedule.IsSuppressedByArguments(arguments))
        {
            return false;
        }

        var sequence = new RetailStartupSequence
        {
            Name = "Level100IntroCutscene",
            PlaybackRetirement = PlaybackRetirement,
        };
        sequence.InitializeForClip(
            RetailStartupSequence.ResolveMediaRoot(arguments),
            RetailStartupCue.Level100IntroCutscene,
            // A capture run is deterministic by contract, so the movie has to
            // advance on the tick rather than on a delta the host could jitter.
            // In practice IsSuppressedByArguments already excludes capture runs
            // unless --intro forces them; this keeps the two decisions from
            // drifting apart.
            IsCaptureRun(arguments)
                ? RetailStartupClockMode.FixedTick
                : RetailStartupClockMode.Wall);

        if (sequence.ScheduledSeconds <= 0d)
        {
            GD.PushWarning(
                "The Level 100 intro cutscene (data/video/cutscenes/01.vid) is not " +
                "decoded, so it does not play. Run " +
                "`python ./rebuild/tools/materialize_retail_assets.py --startup-media`. " +
                (sequence.MediaUnavailableReason ?? "The cue had no decoded media."));
            sequence.QueueFree();
            return false;
        }

        _session.BeginLevel100IntroCutscene();
        sequence.Completed += FinishLevel100IntroCutscene;
        _introCutscene = sequence;
        AddChild(sequence);
        QueueRedraw();
        return true;
    }

    /// <summary>
    /// Hands the screen to gameplay when the movie ends or the player aborts it.
    /// Retail makes no distinction: <c>PlayFullscreen</c> returns either way and
    /// <c>RestartLoopRunLevel</c> resumes at <c>game.cpp:1342</c>.
    /// </summary>
    private void FinishLevel100IntroCutscene()
    {
        if (_session.Screen != RetailFrontendScreen.IntroCutscene)
        {
            return;
        }

        if (!RetailFrontendScenePath.TryCompleteIntroCutscene(
                _session,
                startupMediaActive: false))
        {
            return;
        }
        _introCutscene?.QueueFree();
        _introCutscene = null;

        // Raise the existing activation edge NOW rather than on the next frame.
        // This runs from the cutscene node's _Process, which Godot schedules
        // after this node's, so deferring it would leave the session on Gameplay
        // for one frame with nothing activated.
        if (!TryRaiseGameplayActivation())
        {
            QueueRedraw();
        }
    }

    private static bool IsCaptureRun(IReadOnlyList<string> arguments)
    {
        foreach (string argument in arguments)
        {
            if (argument.StartsWith("--capture-dir=", StringComparison.Ordinal) ||
                argument.StartsWith("--capture-plan=", StringComparison.Ordinal) ||
                argument.StartsWith("--capture-size=", StringComparison.Ordinal) ||
                argument.StartsWith("--capture-offsets-ms=", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
