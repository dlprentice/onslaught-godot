// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// Deterministic shared music state above a platform stream. Filesystem discovery,
/// decoder timing, and device callbacks remain presentation-adapter concerns.
/// The state/order follows <c>references/Onslaught/Music.h:48-106</c> and
/// <c>Music.cpp:111-573</c>, with the measured PC release differences recorded in
/// <c>cmusic-shared-semantics-2026-08-11.*</c>: Ogg discovery, the compiled null
/// assignment to random mode, and ToEven <c>round(volume * 127)</c> conversion.
/// </summary>
public sealed class RetailMusicPolicy
{
    // CCareer's authored default is retained as the raw option value. CMusic
    // separately seeds current/target to 127 before restoring that option.
    public const float AuthoredDefaultVolume = 0.9f;
    public const int FullVolume = 127;
    // CMusic::FadeVolumes snaps a distance below ten, otherwise moves by five.
    public const int FadeStep = 5;
    // Released CMusic::AddDirectoryToPlaylist uses one literal "ogg" call.
    public const string PlaylistExtension = "ogg";

    public RetailMusicPolicy()
    {
        ConfiguredVolume = AuthoredDefaultVolume;
        SetVolume = RoundVolume(ConfiguredVolume);
        CurrentVolume = FullVolume;
        TargetVolume = FullVolume;
    }

    public float ConfiguredVolume { get; private set; }

    public int SetVolume { get; private set; }

    public int CurrentVolume { get; private set; }

    public int TargetVolume { get; private set; }

    public bool IsPlaying { get; private set; }

    public RetailMusicPlayType PlayType { get; private set; } =
        RetailMusicPlayType.Linear;

    public string? CurrentTrackIdentity { get; private set; }

    public string? QueuedTrackIdentity { get; private set; }

    public RetailMusicSelection? Selection { get; private set; }

    public string? SelectionTrackIdentity { get; private set; }

    public void SetConfiguredVolume(float value)
    {
        if (!float.IsFinite(value) || value is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Music option values must be finite and between zero and one.");
        }

        ConfiguredVolume = value;
        SetVolume = RoundVolume(value);
    }

    public static int TrackIndex(RetailMusicSelection selection) => selection switch
    {
        RetailMusicSelection.Frontend => 8,
        RetailMusicSelection.Tutorial => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(selection)),
    };

    public IReadOnlyList<RetailMusicAction> PlaySelection(
        RetailMusicSelection selection,
        string trackIdentity,
        bool fade = false)
    {
        if (IsPlaying && fade)
        {
            if (!StringComparer.Ordinal.Equals(trackIdentity, CurrentTrackIdentity))
            {
                QueuedTrackIdentity = trackIdentity;
                TargetVolume = 0;
            }
            return [];
        }

        PlayType = RetailMusicPlayType.Selection;
        Selection = selection;
        SelectionTrackIdentity = trackIdentity;
        return StartTrack(trackIdentity);
    }

    public IReadOnlyList<RetailMusicAction> PlayFromList(
        string? requestedTrackIdentity,
        string? randomTrackIdentity,
        bool fade = true)
    {
        if (IsPlaying && fade)
        {
            if (!StringComparer.Ordinal.Equals(
                    requestedTrackIdentity,
                    CurrentTrackIdentity))
            {
                QueuedTrackIdentity = requestedTrackIdentity;
                TargetVolume = 0;
            }
            return [];
        }

        string trackIdentity;
        if (requestedTrackIdentity is null)
        {
            // This is intentionally assignment, not a corrected comparison:
            // retail compiled mPlayType=MPT_RANDOM on the direct null path.
            PlayType = RetailMusicPlayType.Random;
            Selection = null;
            SelectionTrackIdentity = null;
            trackIdentity = randomTrackIdentity ?? throw new ArgumentNullException(
                nameof(randomTrackIdentity));
        }
        else
        {
            trackIdentity = requestedTrackIdentity;
        }

        return StartTrack(trackIdentity);
    }

    public IReadOnlyList<RetailMusicAction> AdvanceFadeStep()
    {
        if (!IsPlaying)
        {
            return [];
        }

        if (Math.Abs(CurrentVolume - TargetVolume) < 10)
        {
            CurrentVolume = TargetVolume;
        }
        if (CurrentVolume < TargetVolume)
        {
            CurrentVolume += FadeStep;
        }
        if (CurrentVolume > TargetVolume)
        {
            CurrentVolume -= FadeStep;
        }

        var actions = new List<RetailMusicAction>
        {
            new(RetailMusicActionKind.SetVolume, null, CurrentVolume),
        };
        if (CurrentVolume == 0 && QueuedTrackIdentity is not null)
        {
            string queuedTrackIdentity = QueuedTrackIdentity;
            QueuedTrackIdentity = null;
            actions.AddRange(StartTrack(queuedTrackIdentity));
            return actions;
        }

        if (CurrentVolume == TargetVolume)
        {
            TargetVolume = SetVolume;
        }
        return actions;
    }

    public IReadOnlyList<RetailMusicAction> HandleTrackFinished()
    {
        if (!IsPlaying ||
            PlayType != RetailMusicPlayType.Selection ||
            Selection is null ||
            SelectionTrackIdentity is null)
        {
            return [];
        }

        return StartTrack(SelectionTrackIdentity);
    }

    public IReadOnlyList<RetailMusicAction> Kill()
    {
        IReadOnlyList<RetailMusicAction> actions = IsPlaying
            ? [new RetailMusicAction(RetailMusicActionKind.Stop, null, 0)]
            : [];
        IsPlaying = false;
        PlayType = RetailMusicPlayType.Linear;
        CurrentTrackIdentity = null;
        QueuedTrackIdentity = null;
        Selection = null;
        SelectionTrackIdentity = null;
        CurrentVolume = SetVolume;
        TargetVolume = SetVolume;
        return actions;
    }

    public IReadOnlyList<RetailMusicAction> Reset()
    {
        IReadOnlyList<RetailMusicAction> actions = Kill();
        ConfiguredVolume = AuthoredDefaultVolume;
        SetVolume = RoundVolume(ConfiguredVolume);
        CurrentVolume = FullVolume;
        TargetVolume = FullVolume;
        return actions;
    }

    public RetailMusicPolicySnapshot Snapshot() => new(
        ConfiguredVolume,
        SetVolume,
        CurrentVolume,
        TargetVolume,
        IsPlaying,
        PlayType,
        CurrentTrackIdentity,
        QueuedTrackIdentity,
        Selection,
        SelectionTrackIdentity);

    private IReadOnlyList<RetailMusicAction> StartTrack(string trackIdentity)
    {
        var actions = new List<RetailMusicAction>();
        if (IsPlaying)
        {
            actions.Add(new RetailMusicAction(RetailMusicActionKind.Stop, null, 0));
        }

        CurrentVolume = SetVolume;
        TargetVolume = SetVolume;
        CurrentTrackIdentity = trackIdentity;
        QueuedTrackIdentity = null;
        IsPlaying = true;

        actions.Add(new RetailMusicAction(
            RetailMusicActionKind.SetVolume,
            null,
            CurrentVolume));
        actions.Add(new RetailMusicAction(
            RetailMusicActionKind.Play,
            trackIdentity,
            0));
        return actions;
    }

    private static int RoundVolume(float value) => checked((int)MathF.Round(
        value * FullVolume,
        MidpointRounding.ToEven));
}

public readonly record struct RetailMusicPolicySnapshot(
    float ConfiguredVolume,
    int SetVolume,
    int CurrentVolume,
    int TargetVolume,
    bool IsPlaying,
    RetailMusicPlayType PlayType,
    string? CurrentTrackIdentity,
    string? QueuedTrackIdentity,
    RetailMusicSelection? Selection,
    string? SelectionTrackIdentity);

public enum RetailMusicSelection
{
    Frontend = 0,
    Tutorial = 2,
}

public enum RetailMusicPlayType
{
    Single = 0,
    Linear = 1,
    Random = 2,
    Selection = 3,
}

public enum RetailMusicActionKind
{
    SetVolume = 0,
    Stop = 1,
    Play = 2,
}

public readonly record struct RetailMusicAction(
    RetailMusicActionKind Kind,
    string? TrackIdentity,
    int Volume);
