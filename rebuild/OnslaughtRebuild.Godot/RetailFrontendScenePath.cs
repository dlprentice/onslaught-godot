// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The Godot scene's player-visible frontend path: Lost Toys / opening
/// FMV / splash skip, then CFEPIntro click-to-start, then CFEPMain row
/// accept (Campaign / Load / Options / Exit), then Options apply pulse and
/// dropdown confirm / right-click cancel, then New Game campaign accept
/// (DevSelect / LevelSelect / Briefing / Config / Loading), campaign
/// Back, QuitConfirm Yes/No, then Loading → Gameplay (and the
/// intro-cutscene handoff).
///
/// <para>No Godot types. <see cref="RetailFrontendFlow"/> and
/// <see cref="RetailStartupSequence"/> call the same accept/skip predicates
/// the focused test drives, so the host is not a second copy of the
/// session Confirm path.</para>
/// </summary>
public sealed class RetailFrontendScenePath
{
    public bool StartupMediaActive { get; private set; }

    public static bool IsStartupSuppressed(IReadOnlyList<string> arguments) =>
        RetailStartupSchedule.IsSuppressedByArguments(arguments);

    public static bool AcceptsStartupSkip(bool left, bool middle, bool right, int dik) =>
        RetailFmvSkip.AcceptsMouseLatch(left, middle, right)
        || RetailFmvSkip.AcceptsDefaultSkipScanCode(dik);

    public static bool AcceptsClickToStartMouse(
        RetailFrontendScreen screen,
        float x,
        float y) =>
        screen == RetailFrontendScreen.ClickToStart
        && RetailClickToStartInput.AcceptsMouseAt(x, y);

    public static bool AcceptsClickToStartKey(RetailFrontendScreen screen, int dik) =>
        screen == RetailFrontendScreen.ClickToStart
        && RetailClickToStartInput.AcceptsDefaultConfirmScanCode(dik);

    public static bool CanAcceptMainMenuRow(RetailFrontendSession session, int index)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Screen == RetailFrontendScreen.MainMenu
            && index >= 0
            && index < session.Items.Count
            && session.Items[index].IsAvailable;
    }

    public void Begin(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        StartupMediaActive = !IsStartupSuppressed(arguments);
    }

    public void CompleteStartup() => StartupMediaActive = false;

    public bool TrySkipStartup(bool left, bool middle, bool right, int dik)
    {
        if (!StartupMediaActive || !AcceptsStartupSkip(left, middle, right, dik))
        {
            return false;
        }

        StartupMediaActive = false;
        return true;
    }

    public bool TryAcceptClickToStartMouse(RetailFrontendSession session, float x, float y)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (StartupMediaActive || !AcceptsClickToStartMouse(session.Screen, x, y))
        {
            return false;
        }

        return session.Confirm() == RetailFrontendSignal.PageChanged
            && session.Screen == RetailFrontendScreen.MainMenu;
    }

    public bool TryAcceptClickToStartKey(RetailFrontendSession session, int dik)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (StartupMediaActive || !AcceptsClickToStartKey(session.Screen, dik))
        {
            return false;
        }

        return session.Confirm() == RetailFrontendSignal.PageChanged
            && session.Screen == RetailFrontendScreen.MainMenu;
    }

    public bool TryAcceptMainMenuRow(RetailFrontendSession session, int index)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (StartupMediaActive || !CanAcceptMainMenuRow(session, index))
        {
            return false;
        }

        session.SelectMainIndex(index);
        return TryConfirmPage(session, StartupMediaActive, out RetailFrontendSignal signal)
            && signal == RetailFrontendSignal.PageChanged;
    }

    public bool TryAcceptDevSelect(RetailFrontendSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Screen != RetailFrontendScreen.DevSelect)
        {
            return false;
        }

        return TryConfirmPage(session, StartupMediaActive, out RetailFrontendSignal signal)
            && signal is RetailFrontendSignal.PageChanged or RetailFrontendSignal.CareerLoadRequested
            && session.Screen == RetailFrontendScreen.LevelSelect;
    }

    public bool TryAcceptLevelSelect(RetailFrontendSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Screen != RetailFrontendScreen.LevelSelect)
        {
            return false;
        }

        return TryConfirmPage(session, StartupMediaActive, out RetailFrontendSignal signal)
            && signal == RetailFrontendSignal.PageChanged
            && session.Screen == RetailFrontendScreen.MissionBriefing;
    }

    public bool TryAcceptMissionBriefing(RetailFrontendSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Screen != RetailFrontendScreen.MissionBriefing)
        {
            return false;
        }

        return TryConfirmPage(session, StartupMediaActive, out RetailFrontendSignal signal)
            && signal == RetailFrontendSignal.PageChanged
            && session.Screen == RetailFrontendScreen.SelectConfiguration;
    }

    public bool TryAcceptSelectConfiguration(
        RetailFrontendSession session,
        out RetailFrontendSignal signal)
    {
        ArgumentNullException.ThrowIfNull(session);
        signal = RetailFrontendSignal.None;
        if (session.Screen != RetailFrontendScreen.SelectConfiguration)
        {
            return false;
        }

        return TryConfirmPage(session, StartupMediaActive, out signal)
            && signal == RetailFrontendSignal.LevelLaunchRequested
            && session.Screen == RetailFrontendScreen.Loading;
    }

    public bool TryAcceptQuitConfirm(
        RetailFrontendSession session,
        out RetailFrontendSignal signal)
    {
        ArgumentNullException.ThrowIfNull(session);
        signal = RetailFrontendSignal.None;
        if (session.Screen != RetailFrontendScreen.QuitConfirm)
        {
            return false;
        }

        return TryConfirmPage(session, StartupMediaActive, out signal)
            && signal is RetailFrontendSignal.PageChanged or RetailFrontendSignal.ExitRequested;
    }

    public bool TryCompleteLoading(RetailFrontendSession session, bool launchConsumed)
    {
        ArgumentNullException.ThrowIfNull(session);
        return TryCompleteLoading(session, StartupMediaActive, launchConsumed);
    }

    /// <summary>
    /// The host Loading → Gameplay handoff. <c>RetailFrontendFlow._Process</c>
    /// calls this after the launch request is consumed and the world is ready,
    /// when there is no intro cutscene to play.
    /// </summary>
    public static bool TryCompleteLoading(
        RetailFrontendSession session,
        bool startupMediaActive,
        bool launchConsumed)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (startupMediaActive
            || !launchConsumed
            || session.Screen != RetailFrontendScreen.Loading)
        {
            return false;
        }

        session.CompleteLevel100Load();
        return session.Screen == RetailFrontendScreen.Gameplay;
    }

    public bool TryCompleteIntroCutscene(RetailFrontendSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return TryCompleteIntroCutscene(session, StartupMediaActive);
    }

    /// <summary>
    /// The host intro-cutscene → Gameplay handoff. Retail treats play-out and
    /// abort the same; <c>RetailFrontendFlow.FinishLevel100IntroCutscene</c>
    /// calls this.
    /// </summary>
    public static bool TryCompleteIntroCutscene(
        RetailFrontendSession session,
        bool startupMediaActive)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (startupMediaActive || session.Screen != RetailFrontendScreen.IntroCutscene)
        {
            return false;
        }

        session.CompleteLevel100IntroCutscene();
        return session.Screen == RetailFrontendScreen.Gameplay;
    }

    /// <summary>
    /// The host post-Won re-entry. Lands on the released debriefing page with
    /// the FillOut Won update already applied.
    /// </summary>
    public static bool TryAcceptWonHandoff(
        RetailFrontendSession session,
        Level100MissionOutcome outcome,
        Level100MissionTerminalState terminalState,
        IReadOnlyList<int>? baseThingsLeft = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.TryAcceptWonHandoff(outcome, terminalState, baseThingsLeft)
            && session.Screen == RetailFrontendScreen.Debriefing;
    }

    public bool TryBack(RetailFrontendSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return TryBackPage(session, StartupMediaActive, out RetailFrontendSignal signal)
            && signal == RetailFrontendSignal.PageChanged;
    }

    /// <summary>
    /// The host Back path. Campaign chevrons and Escape call this from
    /// <c>RetailFrontendFlow</c>; Options still pops its own stack first.
    /// </summary>
    public static bool TryBackPage(
        RetailFrontendSession session,
        bool startupMediaActive,
        out RetailFrontendSignal signal)
    {
        ArgumentNullException.ThrowIfNull(session);
        signal = RetailFrontendSignal.None;
        if (startupMediaActive)
        {
            return false;
        }

        switch (session.Screen)
        {
            case RetailFrontendScreen.QuitConfirm:
            case RetailFrontendScreen.DevSelect:
            case RetailFrontendScreen.Options:
            case RetailFrontendScreen.Debriefing:
            case RetailFrontendScreen.SelectConfiguration:
            case RetailFrontendScreen.MissionBriefing:
            case RetailFrontendScreen.LevelSelect:
                signal = session.Back();
                return signal != RetailFrontendSignal.None;
            default:
                return false;
        }
    }

    /// <summary>
    /// The host Confirm path. Click-to-start and main-menu still gate with
    /// <see cref="AcceptsClickToStartMouse"/> / <see cref="CanAcceptMainMenuRow"/>
    /// first; campaign and quit pages call this from
    /// <c>RetailFrontendFlow.Confirm</c>.
    /// </summary>
    public static bool TryConfirmPage(
        RetailFrontendSession session,
        bool startupMediaActive,
        out RetailFrontendSignal signal)
    {
        ArgumentNullException.ThrowIfNull(session);
        signal = RetailFrontendSignal.None;
        if (startupMediaActive)
        {
            return false;
        }

        switch (session.Screen)
        {
            case RetailFrontendScreen.ClickToStart:
            case RetailFrontendScreen.MainMenu:
            case RetailFrontendScreen.QuitConfirm:
            case RetailFrontendScreen.DevSelect:
            case RetailFrontendScreen.Debriefing:
            case RetailFrontendScreen.LevelSelect:
            case RetailFrontendScreen.MissionBriefing:
            case RetailFrontendScreen.SelectConfiguration:
                signal = session.Confirm();
                return signal != RetailFrontendSignal.None;
            default:
                return false;
        }
    }

    /// <summary>
    /// Right-mouse latch SET into the expanded-list cancel leftover.
    /// The helper fields sit idle on the frontend page.
    /// </summary>
    public static bool AcceptsOptionsPointerCancel(bool rightDown) =>
        RetailOptionsDropdownListCancel.Applies(
            helperNonzero: false,
            latch: RetailFrontendLatchToButton.Set(rightDown));

    public static bool ApplyPulseIsPending(RetailOptionsMenu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        return RetailOptionsApplyPulse.ShouldPulse(menu.HasPendingChanges);
    }

    public bool TryConfirmOptions(RetailOptionsMenu menu, out RetailOptionsSignal signal) =>
        TryConfirmOptions(menu, StartupMediaActive, out signal);

    public static bool TryConfirmOptions(
        RetailOptionsMenu menu,
        bool startupMediaActive,
        out RetailOptionsSignal signal)
    {
        ArgumentNullException.ThrowIfNull(menu);
        signal = RetailOptionsSignal.None;
        if (startupMediaActive)
        {
            return false;
        }

        signal = menu.Confirm();
        return signal != RetailOptionsSignal.None;
    }

    public bool TryCancelOptionsDropdown(RetailOptionsMenu menu, bool rightDown)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (StartupMediaActive || !AcceptsOptionsPointerCancel(rightDown))
        {
            return false;
        }

        return menu.CancelExpanded();
    }
}
