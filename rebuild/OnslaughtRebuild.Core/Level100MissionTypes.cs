// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

public enum Level100MissionOutcome
{
    Running = 0,
    Won = 1,
    Lost = 2,
}

public enum Level100MissionTerminalState
{
    None = 0,
    SuccessCountdown = 1,
    FrontEndHandoffReady = 2,
    FailureCountdown = 3,
    FailureMenuReady = 4,
    FailureCountdownElapsed = 5,
}

public enum Level100MissionFailureReason
{
    None = 0,
    TutorialBroken = 1,
    PlayerDeath = 2,
    WaterLoss = 3,
}

public enum Level100MissionJetModeState
{
    NotInJetMode = 0,
    InJetMode = 1,
}

public enum Level100MissionTrigger
{
    TargetZone1 = 1,
    FiringRange = 2,
    TargetZone2 = 3,
    TargetZone3 = 4,
    TargetZone4 = 5,
}

public enum Level100MissionTargetGroup
{
    None = 0,
    StaticTargets = 1,
    TargetTrucks = 2,
    MovingTargets = 3,
    AirborneTargets1 = 4,
    AirborneTargets2 = 5,
    AirTrainer = 6,
}

public enum Level100MissionWeapon
{
    PulseCannonPod = 1,
    MechTwinVulcanCannon = 2,
    MechVulcanCannon = 3,
    MissilePod = 4,
}

public enum Level100MissionWeaponAvailability
{
    Unchanged = 0,
    Disabled = 1,
    Enabled = 2,
}

public enum Level100ScriptValueType
{
    Unset = 0,
    Integer = 1,
    Float = 2,
    String = 3,
    Boolean = 4,
    Thing = 5,
    Position = 6,
}

public enum Level100ScriptWaitKind
{
    None = 0,
    Pause = 1,
    CharacterMessage = 2,
}

public enum Level100PrimaryObjectiveStatus
{
    Uninitialized = 0,
    Failed = 1,
    Complete = 2,
}

/// <summary>
/// Released <c>CMissionObjective</c> status values for the secondary-objective
/// array. These values deliberately do not reuse
/// <see cref="Level100PrimaryObjectiveStatus"/>, whose reconstruction-facing
/// enum orders Failed and Complete differently.
/// </summary>
public enum RetailSecondaryObjectiveStatus
{
    NotDefined = 0,
    Complete = 1,
    Failed = 2,
}

/// <summary>
/// One exact slot of <c>CGame::mSecondaryObjectives</c>. <paramref name="Index"/>
/// is the zero-based array index consumed by Stuart's source and the retail
/// <c>base + index*8</c> native body; <paramref name="TextId"/> is the second
/// dword of that eight-byte record.
/// </summary>
public readonly record struct RetailSecondaryObjectiveSnapshot(
    int Index,
    int TextId,
    RetailSecondaryObjectiveStatus Status);

/// <summary>
/// Deterministic owner for the released ten-entry secondary-objective array.
/// Native 84 and native 88 write the measured complete and failed states into
/// this same distinct array.
/// </summary>
internal sealed class RetailSecondaryObjectiveState
{
    internal const int Count = 10;

    private readonly RetailSecondaryObjectiveSnapshot[] _objectives =
        Enumerable.Range(0, Count)
            .Select(index => new RetailSecondaryObjectiveSnapshot(
                index,
                0,
                RetailSecondaryObjectiveStatus.NotDefined))
            .ToArray();

    internal IReadOnlyList<RetailSecondaryObjectiveSnapshot> Snapshot =>
        Array.AsReadOnly(_objectives.ToArray());

    internal void SetComplete(int index, int textId) =>
        Set(index, textId, RetailSecondaryObjectiveStatus.Complete);

    internal void SetFailed(int index, int textId) =>
        Set(index, textId, RetailSecondaryObjectiveStatus.Failed);

    private void Set(
        int index,
        int textId,
        RetailSecondaryObjectiveStatus status)
    {
        if ((uint)index >= Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                $"Secondary-objective index {index} is outside the released ten-slot array.");
        }

        _objectives[index] = new RetailSecondaryObjectiveSnapshot(
            index,
            textId,
            status);
    }
}

public enum Level100ActorCommand
{
    Activate = 1,
    Deactivate = 2,
    SetObjective = 3,
    UnsetObjective = 4,
}

public enum Level100MissionInput
{
    BrokeTutorial = 1,
    HitFriendlyBuilding = 2,
    FriendlyBuildingDestroyed = 3,
    DestroyedFriendlyBuilding = 4,
    FlashThings = 5,
    HelpPlayer = 6,
    AbortAirborneDrones = 7,
    EvadeFailed = 8,
}

public readonly record struct Level100TutorialProgress(
    bool Introduction,
    bool PulseCannon,
    bool VulcanCannon,
    bool StatusBars);

public readonly record struct Level100ScriptValueSnapshot(
    Level100ScriptValueType Type,
    int Scalar,
    int ComponentY,
    int ComponentZ,
    string? Text);

public sealed record Level100ScriptLocalSnapshot(
    int Ordinal,
    string Name,
    Level100ScriptValueSnapshot Value);

public sealed record Level100QueuedEventSnapshot(
    long Sequence,
    string EventName);

public sealed record Level100ScriptExecutionSnapshot(
    string? EventName,
    int InstructionPointer,
    int Flags,
    int SavedStackSize,
    bool Abort,
    Level100ScriptValueSnapshot? CallContext,
    IReadOnlyList<Level100ScriptValueSnapshot> Stack,
    IReadOnlyList<int> CallFrames);

public sealed record Level100ScriptContinuationSnapshot(
    long Sequence,
    int DueTick,
    Level100ScriptWaitKind WaitKind,
    int WaitArgument,
    Level100ScriptExecutionSnapshot Execution);

public readonly record struct Level100PrimaryObjectiveSnapshot(
    int Objective,
    int TextId,
    Level100PrimaryObjectiveStatus Status);

public sealed record Level100MissionSnapshot(
    int Tick,
    string ProgramSha256,
    int WorldNumber,
    bool InitializerRan,
    bool IsRunning,
    long NextSequence,
    Level100ScriptExecutionSnapshot ActiveExecution,
    IReadOnlyList<Level100ScriptLocalSnapshot> Locals,
    IReadOnlyList<Level100QueuedEventSnapshot> EventQueue,
    IReadOnlyList<Level100ScriptContinuationSnapshot> Continuations,
    Level100MissionOutcome Outcome,
    Level100MissionTerminalState TerminalState,
    Level100MissionFailureReason FailureReason,
    int FailureTextId,
    int TerminalTicksRemaining,
    int InitialPlayerHealth,
    int LatestPlayerHealth,
    int ObservedPlayerHealth,
    bool PlayerActive,
    bool FlightModeEnabled,
    Level100MissionWeaponAvailability PulseCannonAvailability,
    Level100MissionWeaponAvailability TwinVulcanAvailability,
    Level100MissionWeaponAvailability MechVulcanAvailability,
    Level100MissionWeaponAvailability MissilePodAvailability,
    string? NavigationObjective,
    bool Evaded,
    bool Aborted,
    int FriendlyBuildingHits,
    int ScoreDelta,
    Level100TutorialProgress TutorialProgress,
    IReadOnlyList<Level100PrimaryObjectiveSnapshot> PrimaryObjectives,
    IReadOnlyList<RetailSecondaryObjectiveSnapshot> SecondaryObjectives,
    IReadOnlyList<Level100MissionEvent> PendingEvents,
    int MessageClearTick,
    IReadOnlyList<Level100MessageRequested> PendingMessages,
    /// <summary>
    /// The tick <c>ALLOWED_TO_PLAY_MESSAGES</c> lands, i.e.
    /// <c>StartPlayingState + NEXT_FRAME</c>
    /// (<c>references/Onslaught/game.cpp:3025-3031</c>). Equal to
    /// <see cref="Level100MissionTiming.MessageBoxAllowedTick"/> for a pan that
    /// runs its full length, and earlier when the player used
    /// <c>BUTTON_SKIP_PANNING</c>.
    /// </summary>
    int MessageBoxAllowedTick);

public abstract record Level100MissionEvent(int Tick);

public sealed record Level100MessageRequested(
    int Tick,
    int SpeakerId,
    int MessageId,
    bool ScriptWaitsForDuration,
    int ExpectedPlaybackTicks)
    : Level100MissionEvent(Tick);

public sealed record Level100HudEmphasisChanged(int Tick, int PartId, bool Emphasized)
    : Level100MissionEvent(Tick);

public sealed record Level100PlayerActivationChanged(int Tick, bool Active)
    : Level100MissionEvent(Tick);

public sealed record Level100FlightModeAvailabilityChanged(int Tick, bool Enabled)
    : Level100MissionEvent(Tick);

public sealed record Level100WeaponAvailabilityChanged(
    int Tick,
    Level100MissionWeapon Weapon,
    bool Enabled)
    : Level100MissionEvent(Tick);

public sealed record Level100NavigationObjectiveChanged(int Tick, string? ThingName)
    : Level100MissionEvent(Tick);

public sealed record Level100ActorCommandRequested(
    int Tick,
    Level100ActorId ActorId,
    Level100ActorCommand Command)
    : Level100MissionEvent(Tick);

public sealed record Level100SpawnThingRequested(
    int Tick,
    Level100ActorId OwnerActorId,
    string DefinitionName,
    string SpawnerName,
    int Count,
    string ScriptName)
    : Level100MissionEvent(Tick);

public sealed record Level100MissionEventPosted(int Tick, string EventName)
    : Level100MissionEvent(Tick);

public sealed record Level100HelpRequested(int Tick, int HelpMessageId)
    : Level100MissionEvent(Tick);

public sealed record Level100ScoreChanged(int Tick, int Delta, int TotalDelta)
    : Level100MissionEvent(Tick);

/// <summary>
/// <c>IScript::SetSlotSave</c> (<c>0x00533900</c>) stored a tutorial slot; it
/// also calls <c>CCareer::SetSlot</c> at once, which the career's owner
/// applies from this event (<see cref="RetailSetSlotSave.PersistCareerSlot"/>).
/// </summary>
public sealed record Level100TutorialSlotSaved(int Tick, int Slot, bool Value = true)
    : Level100MissionEvent(Tick);

public sealed record Level100PrimaryObjectiveChanged(
    int Tick,
    int Objective,
    int TextId,
    Level100PrimaryObjectiveStatus Status)
    : Level100MissionEvent(Tick);

public sealed record Level100MissionOutcomeDeclared(
    int Tick,
    Level100MissionOutcome Outcome,
    Level100MissionFailureReason FailureReason,
    int FailureTextId)
    : Level100MissionEvent(Tick);

public sealed record Level100TerminalStateChanged(
    int Tick,
    Level100MissionTerminalState State)
    : Level100MissionEvent(Tick);
