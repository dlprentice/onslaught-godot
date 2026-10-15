// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

public enum Level100PausePage
{
    Root,
    ConfirmRetry,
    ConfirmQuit,
}

public enum Level100PauseAction
{
    None,
    Resume,
    RetryLevel,
    ReturnToFrontend,
}

public enum Level100PauseEntryId
{
    Continue,
    MessageLog,
    Briefing,
    ControllerOptions,
    SoundOptions,
    VideoOptions,
    Retry,
    Quit,
    No,
    Yes,
}

public readonly record struct Level100PauseEntry(
    Level100PauseEntryId Id,
    string Label,
    bool IsEnabled = true);

/// <summary>
/// Presentation-only state for the retained pause root and confirmation flow.
/// Mission panels, settings, audio, cursor policy, and frontend navigation stay
/// with their existing owners. Rows without an integrated owner remain visible
/// but disabled.
/// </summary>
public sealed class Level100PauseMenu
{
    private int _rootSelection;
    private int _returnRootSelection;

    public bool IsOpen { get; private set; }

    public Level100PausePage Page { get; private set; } = Level100PausePage.Root;

    public int SelectedIndex { get; private set; }

    public int UnderlyingRootSelection => _returnRootSelection;

    public IReadOnlyList<Level100PauseEntry> Entries => BuildEntries(Page);

    public IReadOnlyList<Level100PauseEntry> RootEntries => BuildEntries(Level100PausePage.Root);

    public void Open()
    {
        if (IsOpen)
        {
            EnsureEnabledSelection();
            return;
        }

        IsOpen = true;
        Page = Level100PausePage.Root;
        SelectedIndex = 0;
        _rootSelection = 0;
        _returnRootSelection = 0;
    }

    public void Reset()
    {
        IsOpen = false;
        Page = Level100PausePage.Root;
        SelectedIndex = 0;
        _rootSelection = 0;
        _returnRootSelection = 0;
    }

    public bool MoveSelection(int direction)
    {
        if (!IsOpen || direction == 0)
        {
            return false;
        }

        IReadOnlyList<Level100PauseEntry> entries = Entries;
        int step = Math.Sign(direction);
        int candidate = SelectedIndex;
        for (int attempt = 0; attempt < entries.Count; attempt++)
        {
            candidate = (candidate + step + entries.Count) % entries.Count;
            if (entries[candidate].IsEnabled)
            {
                bool moved = candidate != SelectedIndex;
                SetSelection(candidate);
                return moved;
            }
        }

        return false;
    }

    public bool Hover(int index)
    {
        IReadOnlyList<Level100PauseEntry> entries = Entries;
        if (!IsOpen || index < 0 || index >= entries.Count || !entries[index].IsEnabled)
        {
            return false;
        }

        bool moved = index != SelectedIndex;
        SetSelection(index);
        return moved;
    }

    public Level100PauseAction ActivateSelected()
    {
        if (!IsOpen)
        {
            return Level100PauseAction.None;
        }

        Level100PauseEntry entry = Entries[SelectedIndex];
        if (!entry.IsEnabled)
        {
            return Level100PauseAction.None;
        }

        switch (entry.Id)
        {
            case Level100PauseEntryId.Continue:
                return Close(Level100PauseAction.Resume);
            case Level100PauseEntryId.Retry:
                EnterConfirmation(Level100PausePage.ConfirmRetry);
                break;
            case Level100PauseEntryId.Quit:
                EnterConfirmation(Level100PausePage.ConfirmQuit);
                break;
            case Level100PauseEntryId.No:
                ReturnToRoot();
                break;
            case Level100PauseEntryId.Yes when Page == Level100PausePage.ConfirmRetry:
                return Close(Level100PauseAction.RetryLevel);
            case Level100PauseEntryId.Yes when Page == Level100PausePage.ConfirmQuit:
                return Close(Level100PauseAction.ReturnToFrontend);
        }

        return Level100PauseAction.None;
    }

    public Level100PauseAction Cancel()
    {
        if (!IsOpen)
        {
            return Level100PauseAction.None;
        }

        if (Page == Level100PausePage.Root)
        {
            return Close(Level100PauseAction.Resume);
        }

        ReturnToRoot();
        return Level100PauseAction.None;
    }

    private void EnterConfirmation(Level100PausePage page)
    {
        _returnRootSelection = _rootSelection;
        Page = page;
        SelectedIndex = 0;
    }

    private void ReturnToRoot()
    {
        Page = Level100PausePage.Root;
        SelectedIndex = _returnRootSelection;
        _rootSelection = SelectedIndex;
        EnsureEnabledSelection();
    }

    private Level100PauseAction Close(Level100PauseAction action)
    {
        IsOpen = false;
        return action;
    }

    private void SetSelection(int index)
    {
        SelectedIndex = index;
        if (Page == Level100PausePage.Root)
        {
            _rootSelection = index;
        }
    }

    private void EnsureEnabledSelection()
    {
        if (!IsOpen || Entries[SelectedIndex].IsEnabled)
        {
            return;
        }

        MoveSelection(1);
    }

    private IReadOnlyList<Level100PauseEntry> BuildEntries(Level100PausePage page) => page switch
    {
        Level100PausePage.Root =>
        [
            new(Level100PauseEntryId.Continue, "Continue"),
            new(Level100PauseEntryId.MessageLog, "Message Log", false),
            new(Level100PauseEntryId.Briefing, "Briefing", false),
            new(
                Level100PauseEntryId.ControllerOptions,
                "Controller Options",
                false),
            new(Level100PauseEntryId.SoundOptions, "Sound Options", false),
            new(Level100PauseEntryId.VideoOptions, "Video Options", false),
            new(Level100PauseEntryId.Retry, "Retry"),
            new(Level100PauseEntryId.Quit, "Quit"),
        ],
        Level100PausePage.ConfirmRetry or Level100PausePage.ConfirmQuit =>
        [
            new(Level100PauseEntryId.No, "No"),
            new(Level100PauseEntryId.Yes, "Yes"),
        ],
        _ => throw new InvalidOperationException($"Unsupported Level 100 pause page {page}."),
    };
}
