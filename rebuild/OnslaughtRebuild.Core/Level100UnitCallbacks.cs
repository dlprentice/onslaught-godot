// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// A Level 100 thing's native class, as far as its construction draws and its
/// unit callbacks go.
/// </summary>
public enum Level100ConstructionClass : byte
{
    /// <summary><c>CSphereTrigger</c> zones: no draw and no unit callback.</summary>
    Trigger = 0,

    /// <summary><c>CBuilding</c>, with a <c>CUnitAI</c> (or <c>CRepairPadAI</c>, which draws the same).</summary>
    Building = 1,

    /// <summary><c>CCannon</c>: Actor multiplier 4, fire control and an AI.</summary>
    Cannon = 2,

    /// <summary><c>CFeature</c>: the Actor draw only.</summary>
    Feature = 3,

    /// <summary><c>CSimpleBuilding</c>: the Actor draw and 4003, no AI.</summary>
    SimpleBuilding = 4,

    /// <summary>The player's <c>CBattleEngine</c>: the Actor draw and 4003, no AI.</summary>
    BattleEngine = 5,

    /// <summary>A <c>CGroundVehicle</c> wrapped in a one-member <c>CNormalSquad</c>.</summary>
    SquadGroundVehicle = 6,

    /// <summary><c>CDropship</c>.</summary>
    Dropship = 7,

    /// <summary><c>CPlane</c>.</summary>
    Plane = 8,

    /// <summary>
    /// A <c>CSpawnerThing</c> row (World 110 row 5). Its 3000 at −1
    /// (<c>0x004e3322</c>) and its 0.1 s poll while inactive
    /// (<c>0x004e367d-0x004e36a9</c>) draw nothing, so nothing is filed.
    /// </summary>
    SpawnerThing = 9,

    /// <summary>
    /// A <c>CComponent</c> child built inside its parent's construction, a
    /// landing craft's "Dropship Gun Turret": Actor multiplier 1, 4003 and an
    /// AI, and one draw on every Move (<c>0x00428110</c>, <c>0x004284a1</c>).
    /// </summary>
    Component = 10,

    /// <summary>
    /// A <c>CGroundVehicle</c> member of a type-28 <c>CNormalSquad</c>, with
    /// fire control (World 110's Light Gun Tanks and AV-14Bs).
    /// </summary>
    SquadMember = 11,

    /// <summary>
    /// A type-28 <c>CNormalSquad</c> thing: its members' construction, then its
    /// own script, then its 4000, 4001 and 4002.
    /// </summary>
    Squad = 12,
}

/// <summary>One unit's callback state.</summary>
/// <param name="NearCamera">Unit <c>+0x110</c>, rewritten by every 4003.</param>
/// <param name="FirstMoveFrame">
/// The event frame of the unit's first full Move for an Actor multiplier of 4
/// (cannons and ground vehicles), phased by its Actor draw; zero otherwise.
/// </param>
public sealed record Level100UnitCallbackSnapshot(
    Level100ActorId ActorId,
    Level100ConstructionClass Class,
    bool NearCamera,
    int ConstructionFrame,
    int FirstMoveFrame);

/// <summary>The construction class of each Level 100 definition.</summary>
/// <remarks>
/// Classes from the RE lane's construction-order contract
/// (<c>reverse-engineering/game-mechanics/level100-construction-order.md</c>,
/// commits <c>b8a19b68</c> and <c>c3fff6c4</c>): base rows 0-2, 13-20 and
/// 23-29 are <c>CBuilding</c>, 3 and 10-12 <c>CCannon</c>, 4-9
/// <c>CFeature</c>, 30-34 <c>CSimpleBuilding</c>; the level world's Target
/// Tanks are squad-wrapped <c>CGroundVehicle</c>s (behaviour 3, selector 2),
/// the Warehouse a <c>CBuilding</c>, the U-17 a <c>CDropship</c> and the Air
/// Trainer a <c>CPlane</c>. The Target Truck and Target Drone share their
/// motion definitions' selectors (2 and 8).
/// </remarks>
public static class Level100ConstructionClasses
{
    public static Level100ConstructionClass Of(string? definitionName) => definitionName switch
    {
        "Control Tower" or "Forseti Pulse Tank Factory" or "Forseti Repair Pad" or
            "Forseti Research Building" or "Forseti Building 1" or "Forseti Building 2" or
            "Forseti Building 3" or "Forseti Solar Pod" or "Forseti Radar Station" or
            "Forseti Light Fighter Airfield" or "Forseti Docks" or "Hangar" or
            "Forseti Tall Building 1" or "Forseti Tall Building 3" or "Warehouse" =>
            Level100ConstructionClass.Building,
        "SAT Turret" or "Blaster Turret" or "Pulse Turret" => Level100ConstructionClass.Cannon,
        "Iceberg 1" or "Iceberg 2" or "Iceberg 3" or "Iceberg 4" => Level100ConstructionClass.Feature,
        "Forseti City Building 1" or "Forseti City Building 2" or "Forseti City Building 3" =>
            Level100ConstructionClass.SimpleBuilding,
        "BattleEngine" or "Battle Engine" => Level100ConstructionClass.BattleEngine,
        "Target Tank" or "Target Truck" => Level100ConstructionClass.SquadGroundVehicle,
        "U-17 Highside Transporter" or "Muspell Light Landing Craft" or "Muspell Light Landing Empty" =>
            Level100ConstructionClass.Dropship,
        "Air Trainer" or "Target Drone" or "Muspell Fighter" or "Muspell Light Fighter" =>
            Level100ConstructionClass.Plane,
        "General Volume" => Level100ConstructionClass.Trigger,
        "Dropship Gun Turret" => Level100ConstructionClass.Component,
        "Light Gun Tank" or "AV-14B Sabre Pulse Tank" => Level100ConstructionClass.SquadMember,
        "Level Actor Type 19" => Level100ConstructionClass.SpawnerThing,
        Level100ActorDefinitionSet.SquadDefinitionName => Level100ConstructionClass.Squad,
        _ => throw new NotSupportedException(
            $"Level 100 construction class is unadmitted for definition '{definitionName}'."),
    };

    internal static bool IsUnit(Level100ConstructionClass kind) =>
        kind is not (Level100ConstructionClass.Trigger or Level100ConstructionClass.Feature or
            Level100ConstructionClass.SpawnerThing or Level100ConstructionClass.Squad);

    /// <summary>A class whose callbacks the unit-callback owner files: every unit, and a squad.</summary>
    internal static bool HasCallbacks(Level100ConstructionClass kind) =>
        IsUnit(kind) || kind == Level100ConstructionClass.Squad;

    internal static bool HasAi(Level100ConstructionClass kind) =>
        kind is Level100ConstructionClass.Building or Level100ConstructionClass.Cannon or
            Level100ConstructionClass.SquadGroundVehicle or Level100ConstructionClass.Dropship or
            Level100ConstructionClass.Plane or Level100ConstructionClass.Component or
            Level100ConstructionClass.SquadMember;

    internal static bool HasFireControl(Level100ConstructionClass kind) =>
        kind is Level100ConstructionClass.Cannon or Level100ConstructionClass.SquadMember;

    /// <summary>
    /// Actor slot 24, the move multiplier: 4.0 for <c>CCannon</c>
    /// (<c>0x0050e940</c>) and <c>CGroundVehicle</c>, 1.0 for the rest
    /// (<c>0x004de700</c>).
    /// </summary>
    internal static int MoveMultiplier(Level100ConstructionClass kind) =>
        kind is Level100ConstructionClass.Cannon or Level100ConstructionClass.SquadGroundVehicle or
            Level100ConstructionClass.SquadMember ? 4 : 1;
}

/// <summary>
/// Level 100's construction draws and every unit's recurring callbacks on the
/// level's one event manager: <c>CUnit</c> 4003, the <c>CUnitAI</c> 3000,
/// 3001 and 3003 loops, the cannons' fire-control 4001 and the tank squads'
/// 4000, 4001 and 4002.
/// </summary>
/// <remarks>
/// <para>
/// Evidence: the RE lane's construction-order contract
/// (<c>reverse-engineering/game-mechanics/level100-construction-order.md</c>)
/// and the final-wave contract's AI-owner, 4003 and fire-control sections
/// (<c>level100-final-drone-wave.md</c>), all static reads of the pristine
/// <c>74154bfa…</c> specimen, with the turret fire-control law also run as an
/// original-code control (21 cases). Construction runs at event time 0.0 in
/// bucket 0, so everything filed here at −1 or at "now" is delivered on frame 1
/// in filing order.
/// </para>
/// <para>
/// Only callbacks that draw from the gameplay stream are filed. Collision
/// readiness, Actor MOVE for the classes above, animation and script-ready
/// callbacks draw nothing (the contract's delivery table), so leaving them out
/// changes no draw and no draw order.
/// </para>
/// </remarks>
public sealed partial class Level100ActorMechanics
{
    private const int UnitListenerBase = 0x2000_0000;
    private const int UnitListenerSlots = 4;
    private const float FireControlScale = 1.52587890625e-06f; // 0x35cccccd, 0.1/65536
    private const float SquadProcessScale = 1.5258789e-07f;

    private enum UnitCallbackOwner
    {
        Unit = 0,
        Ai = 1,
        FireControl = 2,
        Squad = 3,
    }

    private sealed class UnitCallbackState
    {
        internal required Level100ActorId ActorId { get; init; }
        internal required Level100ConstructionClass Class { get; init; }
        internal required int ConstructionFrame { get; init; }
        internal int FirstMoveFrame { get; set; }
        internal bool NearCamera { get; set; }
    }

    private readonly SortedDictionary<int, UnitCallbackState> _unitCallbacks = [];
    private SimVector3? _cameraPosition;

    private static int UnitListener(Level100ActorId actorId, UnitCallbackOwner owner) =>
        checked(UnitListenerBase + (actorId.Value * UnitListenerSlots) + (int)owner);

    /// <summary>A unit's AI listener, for tests that read the first bucket.</summary>
    internal static int UnitAiListener(Level100ActorId actorId) => UnitListener(actorId, UnitCallbackOwner.Ai);

    /// <summary>A unit's own listener (its 4003 and SHUTDOWN), for tests.</summary>
    internal static int UnitOwnListener(Level100ActorId actorId) => UnitListener(actorId, UnitCallbackOwner.Unit);

    /// <summary>An air unit's Actor MOVE listener, for tests.</summary>
    internal static int AirUnitMoveListener(Level100ActorId actorId) => PlaneListener(actorId, 0);

    private static bool IsUnitListener(int listener) =>
        listener >= UnitListenerBase && listener < InfluenceMapListener;

    /// <summary>
    /// The influence map manager's refresh, event 1000 (<c>0x48c120</c>). The
    /// load starts two chains of it; each delivery takes one draw.
    /// </summary>
    internal const int InfluenceMapListener = int.MaxValue - 2;

    /// <summary>
    /// The unit types <c>SpawnInitialThings</c> (<c>0x0050dcb0</c>) warms up
    /// at the end of the load, in its list order: every type named as the
    /// first argument of a <c>SpawnThing</c> call in Level 100's compiled
    /// scripts, prepended as <c>LoadScriptEvents</c> (<c>0x0050bbde</c>) meets
    /// them (the RE lane's construction-order correction).
    /// </summary>
    private static readonly string[] s_warmUpCandidates =
        ["Target Truck", "Target Tank", "Air Trainer", "Target Drone"];

    /// <summary>
    /// A world's warm-up types. World 110's scripts call no <c>SpawnThing</c>,
    /// so it has none (the World 110 construction contract, "Load order").
    /// </summary>
    private static IReadOnlyList<string> WarmUpCandidates(int worldNumber) => worldNumber switch
    {
        100 => s_warmUpCandidates,
        110 => [],
        _ => throw new ArgumentOutOfRangeException(nameof(worldNumber)),
    };

    private static (Level100ActorId ActorId, UnitCallbackOwner Owner) DecodeUnitListener(int listener)
    {
        int offset = listener - UnitListenerBase;
        return (new Level100ActorId(offset / UnitListenerSlots), (UnitCallbackOwner)(offset % UnitListenerSlots));
    }

    internal IReadOnlyList<Level100UnitCallbackSnapshot> UnitCallbackSnapshots =>
        Array.AsReadOnly(_unitCallbacks.Values.Select(state => new Level100UnitCallbackSnapshot(
            state.ActorId, state.Class, state.NearCamera, state.ConstructionFrame, state.FirstMoveFrame)).ToArray());

    /// <summary>
    /// Level 100's load, in the RE lane's order: the base world's pines (one
    /// draw each), its rows 0-34 in file order, then the level world's rows in
    /// file order. Row 0 is the Start, whose <c>SpawnBattleEngine</c> builds
    /// and initialises the Battle Engine inline, so its draws follow base row
    /// 34; <paramref name="battleEngineRefresh"/> takes its 6002 and 6003
    /// draws.
    /// </summary>
    private void ConstructLevel(Action<RetailEventScheduler, Func<int>>? battleEngineRefresh)
    {
        _planeEvents ??= new(useFloat24Arithmetic: true);
        // The base world's pass: the pines, then CInfluenceMapManager::Load
        // (0x0048b010), whose 0x0048b8e0 takes one unconditional draw and
        // starts a 1000 chain; its 1002 draws nothing. Only a definition set
        // that carries the base world's trees represents that pass.
        bool baseWorldPass = _definitions.BaseWorldPineCount > 0;
        for (int pine = 0; pine < _definitions.BaseWorldPineCount; pine++)
        {
            _ = _releasedRandom.Next();
        }
        if (baseWorldPass)
        {
            FileInfluenceMapRefresh(_planeEvents, reuseHandle: -1);
        }

        Dictionary<string, Level100ActorId> byIdentity = _actors.Snapshot.Actors
            .ToDictionary(actor => actor.DefinitionIdentity, actor => actor.ActorId, StringComparer.Ordinal);
        // The level world's script carriers file their INIT_SCRIPT at their own
        // row positions among the level rows (after the rest when the set
        // carries no level-row identities).
        int nextCarrier = 0;
        // Squad members and components are built inside their owners'
        // construction (the World 110 construction contract).
        var builtByOwner = new HashSet<string>(
            _definitions.Squads.SelectMany(squad => squad.MemberIdentities)
                .Concat(_definitions.Components.Select(component => component.ChildIdentity)),
            StringComparer.Ordinal);
        foreach (Level100ActorDefinition definition in _definitions.Actors.OrderBy(item => item.AuthoredOrder))
        {
            if (LevelWorldRow(definition) is { } levelRow)
            {
                FileCarrierScriptInits(ref nextCarrier, levelRow);
            }

            if (builtByOwner.Contains(definition.DefinitionIdentity))
            {
                continue;
            }

            if (!byIdentity.TryGetValue(definition.DefinitionIdentity, out Level100ActorId actorId))
            {
                // A base row the career marks lost: the load skips it, and a
                // lost building leaves landscape damage instead.
                StampLostBaseBuilding(definition);
                continue;
            }

            Level100ConstructionClass kind = Level100ConstructionClasses.Of(definition.DefinitionName);
            if (kind == Level100ConstructionClass.Squad)
            {
                ConstructSquad(actorId, definition.DefinitionIdentity, byIdentity);
                continue;
            }

            if (kind == Level100ConstructionClass.SpawnerThing)
            {
                continue;
            }
            if (kind == Level100ConstructionClass.BattleEngine)
            {
                ConstructUnit(actorId, kind);
                battleEngineRefresh?.Invoke(_planeEvents, _releasedRandom.Next);
                continue;
            }

            if (kind == Level100ConstructionClass.Plane)
            {
                RegisterSpawnedActor(actorId);
                continue;
            }

            ConstructUnit(actorId, kind);
        }

        FileCarrierScriptInits(ref nextCarrier, levelRow: null);
        if (baseWorldPass)
        {
            WarmUpUnusedSpawnTypes();
            // LoadWorld's tail: 0x0048b8e0(0) takes one more draw and starts a
            // second 1000 chain; 0x0048b7d0's 1001 draws nothing.
            FileInfluenceMapRefresh(_planeEvents, reuseHandle: -1);
        }
    }

    /// <summary>
    /// <c>SpawnInitialThings</c>: every warm-up type no load row used
    /// (<c>CUnit::Init</c> marks its type used, <c>0x004f908e</c>) is created
    /// at (256, 256, 0) with no script, fully initialised and shut down at
    /// once, so only its construction draws remain: a ground vehicle's Actor
    /// and hover draws, a plane's Actor and <c>CPlane::Init</c> draws. Its
    /// queued events have no reader and are skipped.
    /// </summary>
    private void WarmUpUnusedSpawnTypes()
    {
        HashSet<string> used = _unitCallbacks.Values
            .Select(unit => _actors.GetActor(unit.ActorId).DefinitionName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        foreach (string name in WarmUpCandidates(_definitions.WorldNumber).Where(name => !used.Contains(name)))
        {
            if (Level100ConstructionClasses.Of(name) is not
                (Level100ConstructionClass.SquadGroundVehicle or Level100ConstructionClass.Plane))
            {
                throw new InvalidOperationException($"Unadmitted warm-up type {name}.");
            }

            // The Actor draw, then the hover draw or CPlane::Init's last draw.
            _ = _releasedRandom.Next();
            _ = _releasedRandom.Next();
        }
    }

    /// <summary>
    /// <c>0x0048b8e0</c>: one draw, then 1000 at now + 1.0 + (r mod
    /// 65536)·2⁻¹⁶ (<c>0x0048bf0f</c>, <c>0x0048bf5a</c>).
    /// </summary>
    private void FileInfluenceMapRefresh(RetailEventScheduler events, int reuseHandle)
    {
        int sample = _releasedRandom.Next() % 65536;
        float delay = (float)RetailFloat24.Add(1.0, RetailFloat24.Multiply(sample, 1.0 / 65536.0));
        events.AddEventTimeFromNow(delay, 1000, InfluenceMapListener, reuseHandle: reuseHandle);
    }

    /// <summary>
    /// The class sequences of the construction-order contract, less the
    /// callbacks that never draw. Called for load rows and for every
    /// <c>SpawnThing</c> result that is not a plane.
    /// </summary>
    private void ConstructUnit(Level100ActorId actorId, Level100ConstructionClass kind)
    {
        RetailEventScheduler events = _planeEvents!;
        // CComplexThing::Init binds the thing's script first (0x004f42da).
        if (HasScript(actorId))
        {
            FileScriptInit(actorId);
        }

        switch (kind)
        {
            case Level100ConstructionClass.Trigger:
                return;
            case Level100ConstructionClass.Feature:
                _ = _releasedRandom.Next();
                return;
        }

        UnitCallbackState state = AddUnitCallbacks(actorId, kind);
        int actorDraw = _releasedRandom.Next();
        if (Level100ConstructionClasses.MoveMultiplier(kind) == 4)
        {
            state.FirstMoveFrame = FirstFullMoveFrame(state.ConstructionFrame, actorDraw);
        }
        if (kind == Level100ConstructionClass.SquadGroundVehicle)
        {
            SeedGroundMovePhase(actorId, state);
        }

        switch (kind)
        {
            case Level100ConstructionClass.Building:
                FileUnitRefresh(events, actorId);
                FileScriptReady(actorId);
                FileInitialAi(events, actorId, _actors.GetActor(actorId).DefinitionName == "Warehouse");
                break;
            case Level100ConstructionClass.Cannon:
                // CUnit::Init calls the fire-control refresh once (0x004f90ce):
                // one draw and 4001, before the 4003 and the AI.
                FileFireControl(events, actorId, _releasedRandom.Next(), reuseHandle: -1);
                FileUnitRefresh(events, actorId);
                FileScriptReady(actorId);
                FileInitialAi(events, actorId, hasTarget: false);
                break;
            case Level100ConstructionClass.SimpleBuilding:
            case Level100ConstructionClass.BattleEngine:
                FileUnitRefresh(events, actorId);
                break;
            case Level100ConstructionClass.SquadGroundVehicle:
                FileUnitRefresh(events, actorId);
                // CGroundUnit::Init's hover draw (0x0047c869): profile +0x10c
                // is 1 from CUnitHover, so +0x25c = (r mod 65536) × 2π/65536.
                _ = _releasedRandom.Next();
                FileScriptReady(actorId);
                FileInitialAi(events, actorId, hasTarget: false);
                // CNormalSquad::Init: 4000, 4001, then slot 66's 4002.
                FileSquadEvent(events, actorId, 4000, -1);
                FileSquadEvent(events, actorId, 4001, -1);
                FileSquadEvent(events, actorId, 4002, -1);
                break;
            case Level100ConstructionClass.Dropship:
                // Its MOVE comes with the Actor draw; a landing craft builds its
                // turret child after both (World 110 contract, "Landing craft
                // and their turrets").
                if (_actors.GetBaseState(actorId).RetailPlane is not null)
                {
                    RegisterDropship(events, actorId);
                }
                ConstructComponents(actorId);
                FileUnitRefresh(events, actorId);
                FileScriptReady(actorId);
                FileInitialAi(events, actorId, hasTarget: false);
                break;
            default:
                throw new InvalidOperationException($"Unadmitted construction class {kind}.");
        }
    }

    /// <summary>
    /// A lost base-world row in the load (<c>0x0050d01f-0x0050d13e</c>; the
    /// World 110 seed contract, "base-world carry-over"). When its class type
    /// has bit <c>0x100</c> (the building setter <c>0x00417660</c> ORs
    /// <c>0x40100120</c>; cannons and features do not), ten iterations each
    /// take two shared draws, the first for Y and the second for X, and stamp
    /// landscape damage type 6 (<c>0x005475d0</c>) at the row's position plus
    /// ((r mod 65536)·2⁻¹⁶ − 0.5) × 5.0 per axis, at single precision.
    /// </summary>
    private void StampLostBaseBuilding(Level100ActorDefinition definition)
    {
        if (Level100ConstructionClasses.Of(definition.DefinitionName) is not
            (Level100ConstructionClass.Building or Level100ConstructionClass.SimpleBuilding))
        {
            return;
        }

        float x = BitConverter.Int32BitsToSingle(definition.AuthoredTransform.RetailPositionFloatBits.X);
        float y = BitConverter.Int32BitsToSingle(definition.AuthoredTransform.RetailPositionFloatBits.Y);
        float Offset(float axis) => (float)RetailFloat24.Add(
            RetailFloat24.Multiply(
                RetailFloat24.Subtract(RetailFloat24.Multiply(_releasedRandom.Next() % 65536, 1.0 / 65536.0), 0.5),
                5.0),
            axis);
        for (int stamp = 0; stamp < 10; stamp++)
        {
            float stampY = Offset(y);
            float stampX = Offset(x);
            _landscapeDamageStamps.Add(new Level100LandscapeDamageStamp(
                BitConverter.SingleToInt32Bits(stampX), BitConverter.SingleToInt32Bits(stampY), 6));
        }
    }

    /// <summary>
    /// A type-28 squad (World 110 construction contract, "Type-28 squads"):
    /// each member in order takes collision, the Actor draw (multiplier 4),
    /// the fire-control draw and 4001, 4003, the hover draw and its AI 3000;
    /// then the squad binds its own script (<c>0x004e61b4</c>) and takes the
    /// draws before its 4000 (<c>0x004e8177</c>) and 4001 (<c>0x004e8486</c>).
    /// When <c>Process</c> returns 0 it also takes 4002's draw
    /// (<c>0x004e709c</c>); otherwise 4002 is queued at −1 with no draw.
    /// </summary>
    private void ConstructSquad(
        Level100ActorId squadId,
        string squadIdentity,
        IReadOnlyDictionary<string, Level100ActorId> byIdentity)
    {
        RetailEventScheduler events = _planeEvents!;
        Level100SquadDefinition squad = _definitions.Squads.Single(item =>
            StringComparer.Ordinal.Equals(item.DefinitionIdentity, squadIdentity));
        foreach (string member in squad.MemberIdentities)
        {
            Level100ActorId memberId = byIdentity[member];
            UnitCallbackState state = AddUnitCallbacks(memberId, Level100ConstructionClass.SquadMember);
            state.FirstMoveFrame = FirstFullMoveFrame(state.ConstructionFrame, _releasedRandom.Next());
            SeedGroundMovePhase(memberId, state);
            FileFireControl(events, memberId, _releasedRandom.Next(), reuseHandle: -1);
            FileUnitRefresh(events, memberId);
            // CGroundUnit::Init's hover draw (0x0047c869).
            _ = _releasedRandom.Next();
            FileInitialAi(events, memberId, hasTarget: false);
        }

        if (HasScript(squadId))
        {
            FileScriptInit(squadId);
        }

        _ = AddUnitCallbacks(squadId, Level100ConstructionClass.Squad);
        FileSquadEvent(events, squadId, 4000, -1);
        FileSquadEvent(events, squadId, 4001, -1);
        if (SquadProcessesAtConstruction(squadIdentity))
        {
            events.AddEvent(4002, UnitListener(squadId, UnitCallbackOwner.Squad), RetailEventScheduler.NextFrame);
        }
        else
        {
            FileSquadEvent(events, squadId, 4002, -1);
        }
    }

    /// <summary>
    /// Whether <c>CSquadNormal::Process</c> returns 1 at construction. It does
    /// only when a third of the members sit more than 1.5 radii from their
    /// formation slots after the squad turns to its first target
    /// (<c>0x004e7cf0</c>, <c>0x004e7f40</c>). Core has no formation or squad
    /// target search yet, so this is the RE lane's static estimate for World
    /// 110 (rows 16 and 18; "Type-28 squads"), an open question until a load
    /// log reads squad <c>+0xc4</c>.
    /// </summary>
    private bool SquadProcessesAtConstruction(string squadIdentity) =>
        _definitions.WorldNumber == 110 &&
        squadIdentity is "wres:rlwd:0016" or "wres:rlwd:0018";

    /// <summary>
    /// A parent's components (<c>CComponent</c>, Init <c>0x00427b80</c>):
    /// collision, the Actor draw with its MOVE for the next frame, 4003, the
    /// animation event and the AI 3000, built after the parent's Actor draw.
    /// </summary>
    private void ConstructComponents(Level100ActorId parentId)
    {
        string parent = _actors.GetActor(parentId).DefinitionIdentity;
        foreach (Level100ComponentDefinition component in _definitions.Components.Where(item =>
                     StringComparer.Ordinal.Equals(item.ParentIdentity, parent)))
        {
            Level100ActorId childId = _actors.Snapshot.Actors.Single(actor =>
                StringComparer.Ordinal.Equals(actor.DefinitionIdentity, component.ChildIdentity)).ActorId;
            RetailEventScheduler events = _planeEvents!;
            _ = AddUnitCallbacks(childId, Level100ConstructionClass.Component);
            _ = _releasedRandom.Next();
            events.AddEvent(ComponentMoveEvent, ComponentMoveListener(childId), RetailEventScheduler.NextFrame);
            FileUnitRefresh(events, childId);
            FileInitialAi(events, childId, hasTarget: false);
        }
    }

    /// <summary>A component's Actor MOVE, re-filed every frame.</summary>
    internal const int ComponentMoveEvent = 3000;

    private const int ComponentMoveListenerBase = 0x1800_0000;

    internal static int ComponentMoveListener(Level100ActorId actorId) =>
        checked(ComponentMoveListenerBase + actorId.Value);

    internal static bool IsComponentMoveListener(int listener) =>
        listener >= ComponentMoveListenerBase && listener < UnitListenerBase;

    /// <summary>
    /// <c>CComponent::Move</c> (<c>0x00428110</c>): on its normal path one
    /// draw (<c>0x004284a1</c>), and the MOVE is filed again for the next frame
    /// until the component is deleted.
    /// </summary>
    private void DispatchComponentMove(RetailEventScheduler events, RetailEventDispatch dispatch)
    {
        var childId = new Level100ActorId(dispatch.Listener - ComponentMoveListenerBase);
        if (_actors.GetActor(childId).Lifecycle == Level100ActorLifecycle.Destroyed)
        {
            return;
        }

        _ = _releasedRandom.Next();
        events.AddEvent(ComponentMoveEvent, dispatch.Listener, RetailEventScheduler.NextFrame,
            reuseHandle: dispatch.Handle);
    }

    /// <summary>
    /// <c>CPlane::Init</c>'s unit callbacks, filed by the plane registration
    /// after its Move and before its guide callbacks: 4003, and later the AI's
    /// 3000 unless a spawner exit gives the AI its 3002 path instead.
    /// </summary>
    private void AddPlaneUnitCallbacks(Level100ActorId actorId)
    {
        _ = AddUnitCallbacks(actorId, Level100ConstructionClass.Plane);
        FileUnitRefresh(_planeEvents!, actorId);
    }

    private UnitCallbackState AddUnitCallbacks(Level100ActorId actorId, Level100ConstructionClass kind)
    {
        var state = new UnitCallbackState
        {
            ActorId = actorId,
            Class = kind,
            ConstructionFrame = checked((int)_planeEvents!.FrameCount),
        };
        if (!_unitCallbacks.TryAdd(actorId.Value, state))
        {
            throw new InvalidOperationException($"Unit {actorId} was constructed twice.");
        }
        return state;
    }

    /// <summary>
    /// <c>CActor::Init</c> (<c>0x004011e0</c>, steps 5-7 of its owner): q = r
    /// mod multiplier; q − 1 &gt; 0 files LF_MOVE with that counter, otherwise
    /// the counter resets to the multiplier and MOVE is filed, both for the
    /// next frame. With multiplier 4 the first full Move is therefore one, two
    /// or three frames after construction for q ∈ {0, 1}, 2 and 3.
    /// </summary>
    private static int FirstFullMoveFrame(int constructionFrame, int actorDraw)
    {
        int q = actorDraw % 4;
        int lowFrequencyMoves = Math.Max(q - 1, 0);
        return checked(constructionFrame + 1 + lowFrequencyMoves);
    }

    private static void FileUnitRefresh(RetailEventScheduler events, Level100ActorId actorId) =>
        events.AddEvent(4003, UnitListener(actorId, UnitCallbackOwner.Unit), RetailEventScheduler.NextFrame);

    /// <summary>
    /// The <c>CUnitAI</c> constructor (<c>0x004fe710</c>): a unit with an
    /// initial target starts in state 0 with 3001, otherwise in state 1 with
    /// 3000, both at the current time.
    /// </summary>
    private static void FileInitialAi(RetailEventScheduler events, Level100ActorId actorId, bool hasTarget) =>
        events.AddEvent(hasTarget ? 3001 : 3000, UnitListener(actorId, UnitCallbackOwner.Ai), events.Time);

    private void FileFireControl(RetailEventScheduler events, Level100ActorId actorId, int draw, int reuseHandle)
    {
        float due = (float)RetailFloat24.Add(RetailFloat24.Multiply(draw % 65536, FireControlScale), events.Time);
        events.AddEvent(4001, UnitListener(actorId, UnitCallbackOwner.FireControl), due, reuseHandle: reuseHandle);
    }

    /// <summary>
    /// One squad refresh with its draw: 4000 at now + 2 + (r mod 65536)·2⁻¹⁵
    /// (<c>0x004e8177</c>), 4001 at now + 1 + (r mod 65536)·2⁻¹⁶
    /// (<c>0x004e8486</c>), and 4002 at now + 0.99 + (r mod 65536)·1.5258789e-7
    /// (<c>0x004e709c</c>) when <c>CSquadNormal::Process</c> returns 0.
    /// </summary>
    private void FileSquadEvent(RetailEventScheduler events, Level100ActorId actorId, int eventNum, int reuseHandle)
    {
        int sample = _releasedRandom.Next() % 65536;
        (double baseDelay, double scale) = eventNum switch
        {
            4000 => (2.0, 1.0 / 32768.0),
            4001 => (1.0, 1.0 / 65536.0),
            4002 => (0.99f, SquadProcessScale),
            _ => throw new ArgumentOutOfRangeException(nameof(eventNum)),
        };
        float delay = (float)RetailFloat24.Add(baseDelay, RetailFloat24.Multiply(sample, scale));
        events.AddEventTimeFromNow(delay, eventNum, UnitListener(actorId, UnitCallbackOwner.Squad),
            reuseHandle: reuseHandle);
    }

    /// <summary>Delivers one unit callback; a deleted unit's callback does nothing.</summary>
    private void DispatchUnitCallback(RetailEventScheduler events, RetailEventDispatch dispatch)
    {
        (Level100ActorId actorId, UnitCallbackOwner owner) = DecodeUnitListener(dispatch.Listener);
        if (!_unitCallbacks.TryGetValue(actorId.Value, out UnitCallbackState? state))
        {
            throw new InvalidOperationException("Unit callback has no owner.");
        }

        Level100ActorSnapshot actor = _actors.GetActor(actorId);
        if (actor.Lifecycle == Level100ActorLifecycle.Destroyed)
        {
            return;
        }

        switch (owner, dispatch.EventNum)
        {
            case (UnitCallbackOwner.Unit, 4003):
                RefreshUnit(events, dispatch, state, actor);
                return;
            case (UnitCallbackOwner.Unit, 2000) when state.Class is Level100ConstructionClass.Dropship or
                Level100ConstructionClass.Plane:
                ShutDownUnit(actorId);
                return;
            case (UnitCallbackOwner.Ai, 3000):
            case (UnitCallbackOwner.Ai, 3001):
                HandleAiEvent(events, dispatch, state, actor);
                return;
            case (UnitCallbackOwner.Ai, 3003):
                events.AddEvent(3000, dispatch.Listener, RetailEventScheduler.NextFrame, reuseHandle: dispatch.Handle);
                return;
            case (UnitCallbackOwner.FireControl, 4001):
                // 0x004fb280 returns at once, without a draw or a requeue,
                // while the unit is dying. No turret or tank here has an AI
                // target, so the aim branch never runs.
                if (actor.Lifecycle != Level100ActorLifecycle.Alive)
                {
                    return;
                }
                FileFireControl(events, actorId, _releasedRandom.Next(), dispatch.Handle);
                return;
            case (UnitCallbackOwner.Squad, 4000):
            case (UnitCallbackOwner.Squad, 4001):
                FileSquadEvent(events, actorId, dispatch.EventNum, dispatch.Handle);
                return;
            case (UnitCallbackOwner.Squad, 4002):
                if (SquadProcessMoves(actorId))
                {
                    events.AddEvent(4002, dispatch.Listener, RetailEventScheduler.NextFrame, reuseHandle: dispatch.Handle);
                }
                else
                {
                    FileSquadEvent(events, actorId, 4002, dispatch.Handle);
                }
                return;
            default:
                throw new InvalidOperationException(
                    $"Unadmitted unit callback {dispatch.EventNum} for {owner}.");
        }
    }

    /// <summary>
    /// <c>CUnit::HandleEvent</c> 4003 (<c>0x004f98a8-0x004f9972</c>): clear
    /// <c>+0x110</c>, set it when player 0's camera is strictly within 50 units
    /// (squared distance below 2500), take one draw and re-file 4003 at now +
    /// 3.0 + (r mod 65536)/65536.
    /// </summary>
    private void RefreshUnit(RetailEventScheduler events, RetailEventDispatch dispatch,
        UnitCallbackState state, Level100ActorSnapshot actor)
    {
        state.NearCamera = false;
        if (_cameraPosition is { } camera)
        {
            SimVector3 position = actor.Pose.PositionMillimeters;
            double dx = (position.X - (double)camera.X) / 1000.0;
            double dy = (position.Y - (double)camera.Y) / 1000.0;
            double dz = (position.Z - (double)camera.Z) / 1000.0;
            state.NearCamera = ((dy * dy) + (dx * dx)) + (dz * dz) < 2500.0;
        }

        int sample = _releasedRandom.Next() % 65536;
        float delay = (float)RetailFloat24.Add(3.0, RetailFloat24.Multiply(sample, 1.0 / 65536.0));
        events.AddEventTimeFromNow(delay, 4003, dispatch.Listener, reuseHandle: dispatch.Handle);
    }

    /// <summary>
    /// <c>CUnitAI::HandleEvent</c> (<c>0x004ff330</c>). A polling owner
    /// (inactive, AI switched off, or deploy state 1 or 2) takes one draw and
    /// files 3003 at now + 2.0 + (r mod 65536)·2⁻¹⁵; 3003 files 3000 for the
    /// next frame. An active owner runs <c>0x004fec60</c>: nothing while it is
    /// dying or in state 0 on 3000; otherwise <c>CUnitAI::Update</c>'s idle
    /// arm takes one draw and re-files 3000 at now + 1.5 + r·2⁻¹⁶ when
    /// <c>+0x110</c> is set, else now + 3.0 + r·2⁻¹⁵ (its target arm: now +
    /// 0.5 + r·2⁻¹⁶). The Warehouse's 3001
    /// (<c>0x004feac0</c>) draws once when no target is found and re-files at
    /// now + 1.0 + r·2⁻¹⁶. No Level 100 profile is Indiscriminate, so the
    /// target selector itself draws nothing.
    /// </summary>
    private void HandleAiEvent(RetailEventScheduler events, RetailEventDispatch dispatch,
        UnitCallbackState state, Level100ActorSnapshot actor)
    {
        _states.TryGetValue(actor.ActorId.Value, out ActorState? mechanics);
        int aiState = mechanics?.AiState ?? 0;
        void Poll()
        {
            int poll = _releasedRandom.Next() % 65536;
            float due = (float)RetailFloat24.Add(RetailFloat24.Add(events.Time, 2.0), RetailFloat24.Multiply(poll, 1.0 / 32768.0));
            events.AddEvent(3003, dispatch.Listener, due, reuseHandle: dispatch.Handle);
        }

        if (actor.Active && aiState == 4 && state.Class == Level100ConstructionClass.Dropship)
        {
            // AI_ONF on a landing craft: the dispatcher runs
            // CDropshipAI::Update (0x004487e0, one draw at 0x0044880f), then
            // polls (the World 110 construction contract's delivery table).
            _ = _releasedRandom.Next();
            Poll();
            return;
        }

        // A unit leaving (+0x244 is 1 or 2) only polls (0x004ff340-0x004ff34f).
        if (!actor.Active || aiState != 0 || mechanics?.PlaneGuide?.SpeedMode is 1 or 2)
        {
            Poll();
            return;
        }

        if (actor.Lifecycle != Level100ActorLifecycle.Alive)
        {
            return;
        }

        switch (state.Class)
        {
            case Level100ConstructionClass.Dropship:
                // CDropshipAI slot 9 (0x00448580) does nothing in landing
                // state 2; an airborne craft's target search draws nothing and
                // its steering is not modeled. Then one draw (0x00448763) and
                // 3000 at ((r mod 65536)·2⁻¹⁶ + 1.0) + now.
                RequeueAi(events, dispatch, 1.0, 1.0 / 65536.0);
                return;
            case Level100ConstructionClass.Plane:
                // CPlaneAI slot 9 (0x004d21c0): CUnitAI::Update's arm draw,
                // then one draw (0x004d2434) and 3000 at ((r mod 65536)·2⁻¹⁸ +
                // 0.25) + now. Which arm the first think takes is open.
                _ = _releasedRandom.Next();
                RequeueAi(events, dispatch, 0.25, 1.0 / 262144.0);
                return;
        }

        int sample = _releasedRandom.Next() % 65536;
        if (dispatch.EventNum == 3001)
        {
            float next = (float)RetailFloat24.Add(RetailFloat24.Add(events.Time, 1.0), RetailFloat24.Multiply(sample, 1.0 / 65536.0));
            events.AddEvent(3001, dispatch.Listener, next, reuseHandle: dispatch.Handle);
            return;
        }

        // Update's target arm (a script's Attack gave the AI a target) takes
        // one draw and returns r/65536 + 0.5; its sweeping sub-arm needs
        // CUnitSweeping, which no Level 100 profile sets.
        bool hasTarget = mechanics?.TargetActorId is not null;
        double delay = hasTarget
            ? RetailFloat24.Add(RetailFloat24.Multiply(sample, 1.0 / 65536.0), 0.5)
            : state.NearCamera
                ? RetailFloat24.Add(RetailFloat24.Multiply(sample, 1.0 / 65536.0), 1.5)
                : RetailFloat24.Add(RetailFloat24.Multiply(sample, 2.0 / 65536.0), 3.0);
        events.AddEvent(3000, dispatch.Listener, (float)RetailFloat24.Add(events.Time, delay),
            reuseHandle: dispatch.Handle);
    }

    private void RequeueAi(RetailEventScheduler events, RetailEventDispatch dispatch, double baseDelay, double scale)
    {
        int sample = _releasedRandom.Next() % 65536;
        float due = (float)RetailFloat24.Add(
            RetailFloat24.Add(RetailFloat24.Multiply(sample, scale), baseDelay), events.Time);
        events.AddEvent(3000, dispatch.Listener, due, reuseHandle: dispatch.Handle);
    }

    /// <summary>
    /// <c>CSquadNormal::Process</c> (<c>0x004e7110</c>) returns 1 only on its
    /// formation path: a destination more than one unit away or an active path.
    /// A squad whose member follows a waypoint has one; a stationary squad does
    /// not. Its target branch is open (the RE lane's squad answer).
    /// </summary>
    private bool SquadProcessMoves(Level100ActorId actorId) =>
        _states.TryGetValue(actorId.Value, out ActorState? state) &&
        state.Intent == Level100ActorCommandIntent.FollowingWaypoint;

    /// <summary>
    /// A ground vehicle's mechanics state exists from its construction, with
    /// its full-guide phase set so that the phase reaches 0 on the Actor
    /// draw's first full Move: the phase is advanced once per frame from the
    /// frame after construction.
    /// </summary>
    private void SeedGroundMovePhase(Level100ActorId actorId, UnitCallbackState unit)
    {
        int ticks = _definitions.FindMotionDefinition(_actors.GetActor(actorId).DefinitionName)?
            .FullGuideBaseTicks ?? throw new InvalidOperationException("A ground vehicle has no guide cadence.");
        if (ticks != 4)
        {
            throw new InvalidOperationException("The ground guide cadence is not the Actor multiplier.");
        }

        int lowFrequencyMoves = unit.FirstMoveFrame - (unit.ConstructionFrame + 1);
        if (!_states.TryGetValue(actorId.Value, out ActorState? state))
        {
            state = new ActorState
            {
                ActorId = actorId,
                Intent = Level100ActorCommandIntent.Stopped,
                Allegiance = _actors.GetAuthoredAllegiance(actorId),
            };
            _states.Add(actorId.Value, state);
        }
        state.GroundFullGuideBaseTickPhase = (ticks - lowFrequencyMoves) % ticks;
    }

    /// <summary>A unit's first Move of any kind is the frame after its construction.</summary>
    private bool ConstructedThisFrame(Level100ActorId actorId) =>
        _planeEvents is not null &&
        _unitCallbacks.TryGetValue(actorId.Value, out UnitCallbackState? state) &&
        state.ConstructionFrame == _planeEvents.FrameCount;

    private void RestoreUnitCallbacks(Level100ActorMechanicsSnapshot snapshot)
    {
        _unitCallbacks.Clear();
        foreach (Level100UnitCallbackSnapshot item in snapshot.UnitCallbacks ?? [])
        {
            if (!Enum.IsDefined(item.Class) || !Level100ConstructionClasses.HasCallbacks(item.Class) ||
                _actors.GetActor(item.ActorId).DefinitionName is not { } name ||
                Level100ConstructionClasses.Of(name) != item.Class ||
                !_unitCallbacks.TryAdd(item.ActorId.Value, new UnitCallbackState
                {
                    ActorId = item.ActorId,
                    Class = item.Class,
                    ConstructionFrame = item.ConstructionFrame,
                    FirstMoveFrame = item.FirstMoveFrame,
                    NearCamera = item.NearCamera,
                }))
            {
                throw new ArgumentException("Level 100 unit callback snapshot is invalid.", nameof(snapshot));
            }
        }
    }

    private bool AdmitsUnitCallback(RetailEventSlotSnapshot slot)
    {
        (Level100ActorId actorId, UnitCallbackOwner owner) = DecodeUnitListener(slot.Listener);
        if (!_unitCallbacks.TryGetValue(actorId.Value, out UnitCallbackState? state))
        {
            return false;
        }

        return (owner, slot.EventNum) switch
        {
            (UnitCallbackOwner.Unit, 4003) => true,
            // A running-out air unit's own SHUTDOWN (slot 116).
            (UnitCallbackOwner.Unit, 2000) => state.Class is Level100ConstructionClass.Dropship or
                Level100ConstructionClass.Plane,
            (UnitCallbackOwner.Ai, 3000 or 3001 or 3003) => Level100ConstructionClasses.HasAi(state.Class),
            (UnitCallbackOwner.FireControl, 4001) => Level100ConstructionClasses.HasFireControl(state.Class),
            (UnitCallbackOwner.Squad, 4000 or 4001 or 4002) =>
                state.Class is Level100ConstructionClass.SquadGroundVehicle or Level100ConstructionClass.Squad,
            _ => false,
        };
    }
}
