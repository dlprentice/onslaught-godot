// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// A level world's script carriers: its <c>CLevelScriptThing</c> rows, whose
/// only construction event is their INIT_SCRIPT (the RE lane's construction
/// contracts). Level 100's are rows 5 and 17
/// (<c>reverse-engineering/game-mechanics/level100-construction-order.md</c>,
/// "Level-world rows"); World 110's are rows 0, 2 and 39
/// (<c>world-110-construction-order.md</c>, "Level-world rows").
/// </summary>
public readonly record struct Level100ScriptCarrier(int LevelRow, string ScriptName);

internal static class Level100ScriptCarriers
{
    private static readonly Level100ScriptCarrier[] s_world100 =
    [
        new(5, "LevelScript"),
        new(17, "Setup"),
    ];

    private static readonly Level100ScriptCarrier[] s_world110 =
    [
        new(0, "LevelScript"),
        new(2, "Setup"),
        new(39, "Weather"),
    ];

    internal static IReadOnlyList<Level100ScriptCarrier> For(int worldNumber) => worldNumber switch
    {
        100 => s_world100,
        110 => s_world110,
        _ => throw new ArgumentOutOfRangeException(nameof(worldNumber), $"World {worldNumber} has no admitted script carriers."),
    };
}

public sealed partial class Level100ActorMechanics
{
    /// <summary>
    /// INIT_SCRIPT: <c>SetScript</c> binds the VM, then files 2001 at −1 for
    /// the thing itself (<c>0x004f42b1-0x004f42da</c>); its <c>init()</c> runs
    /// when that is delivered. A constructed thing's script is bound first in
    /// its construction sequence, and one bound by another script's
    /// <c>SetScript</c> runs a frame later.
    /// </summary>
    internal const int InitScriptEvent = 2001;

    /// <summary>
    /// The AI constructor's 2003 at −1 for a unit with a script: its
    /// <c>ready()</c> (<c>0x005335a0</c>).
    /// </summary>
    internal const int ScriptReadyEvent = 2003;

    private const int ActorScriptListenerBase = 0x0800_0000;
    private const int CarrierScriptListenerBase = 0x0C00_0000;

    internal static int ActorScriptListener(Level100ActorId actorId) =>
        checked(ActorScriptListenerBase + actorId.Value);

    internal static int CarrierScriptListener(int levelRow) =>
        checked(CarrierScriptListenerBase + levelRow);

    internal static bool IsActorScriptListener(int listener) =>
        listener >= ActorScriptListenerBase && listener < CarrierScriptListenerBase;

    internal static bool IsCarrierScriptListener(int listener) =>
        listener >= CarrierScriptListenerBase && listener < RoundListenerBase;

    internal static bool IsScriptListener(int listener) =>
        IsActorScriptListener(listener) || IsCarrierScriptListener(listener);

    internal static Level100ActorId ScriptListenerActor(int listener) =>
        new(listener - ActorScriptListenerBase);

    internal static int CarrierScriptRow(int listener) => listener - CarrierScriptListenerBase;

    /// <summary>A thing's INIT_SCRIPT, for the next frame.</summary>
    internal void FileScriptInit(Level100ActorId actorId) =>
        LevelEvents.AddEvent(InitScriptEvent, ActorScriptListener(actorId), RetailEventScheduler.NextFrame);

    private bool HasScript(Level100ActorId actorId) => _actors.GetActor(actorId).ScriptName is not null;

    /// <summary>A scripted unit's <c>ready()</c>, filed by its AI constructor before its first think.</summary>
    private void FileScriptReady(Level100ActorId actorId)
    {
        if (HasScript(actorId))
        {
            LevelEvents.AddEvent(ScriptReadyEvent, ActorScriptListener(actorId), RetailEventScheduler.NextFrame);
        }
    }

    /// <summary>
    /// The level world's script carriers due before level row
    /// <paramref name="levelRow"/>, or all that remain when it is null.
    /// </summary>
    private void FileCarrierScriptInits(ref int nextCarrier, int? levelRow)
    {
        IReadOnlyList<Level100ScriptCarrier> carriers = Level100ScriptCarriers.For(_definitions.WorldNumber);
        while (nextCarrier < carriers.Count &&
            (levelRow is null || carriers[nextCarrier].LevelRow < levelRow.Value))
        {
            LevelEvents.AddEvent(
                InitScriptEvent,
                CarrierScriptListener(carriers[nextCarrier].LevelRow),
                RetailEventScheduler.NextFrame);
            nextCarrier++;
        }
    }

    /// <summary>A definition's level-world row, from its <c>wres:rlwd:NNNN</c> identity.</summary>
    private static int? LevelWorldRow(Level100ActorDefinition definition) =>
        definition.DefinitionIdentity.StartsWith("wres:rlwd:", StringComparison.Ordinal) &&
        int.TryParse(definition.DefinitionIdentity.AsSpan("wres:rlwd:".Length), out int row)
            ? row
            : null;
}
