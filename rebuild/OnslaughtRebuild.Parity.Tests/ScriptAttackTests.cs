// SPDX-License-Identifier: GPL-3.0-only
// IScript::Attack00535fa0 and CUnit::SetSquadTarget004fda20, matched onslaught-decomp source and pristine instructions.
// Specimen SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
// Authored unit/squad fixtures validate reconstruction branches; the ordinary tutorial replay is a separate gate.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class ScriptAttackTests
{
    private sealed class TestUnit(ThingContext context) : Unit(context);
    private sealed class OtherThing(ThingContext context) : ComplexThing(context, 0);

    private static ThingContext Context()
    {
        var pool = new SPtrSetPool(); pool.Init(128);
        var events = new EventManager(); events.Init(); events.Log = [];
        return new ThingContext(pool, events, new SeedUniformRandomNumberStream(123), null!, new PhysicsRegistry());
    }

    private static TestUnit Unit(ThingContext context, bool ai = true)
    {
        var unit = new TestUnit(context) { UnitData = new UnitData("fixture"u8.ToArray()) };
        unit.SetThingType(0);
        if (ai) unit.AI = new UnitAI(unit, new InitThing());
        return unit;
    }

    private static NormalSquad Squad(ThingContext context)
    {
        var squad = new NormalSquad(context); squad.SetThingType(0); return squad;
    }

    private static IScript Script(ComplexThing owner)
    {
        var code = new ScriptObjectCode
        {
            Instructions = [], EventPcs = Enumerable.Repeat(-1, 13).ToArray(), Symbols = [], NextSymbolIndex = 0,
            EventFunctions = [], Unknown60 = 0, Unknown5c = 0,
        };
        return IScript.Create(owner, code, owner.Context);
    }

    private static void Attack(IScript script, Thing? target)
    {
        ScriptData? result = new IntData(987); ScriptData sentinel = result;
        var argument = new ThingPtrData(target);
        try { script.Attack([argument], 1, ref result); Assert.Same(sentinel, result); }
        finally { argument.Delete(); }
    }

    [Fact]
    public void UnitAttackLocksTargetWithoutQueuingEventsChangingModeOrDrawingRandom()
    {
        ThingContext context = Context(); TestUnit source = Unit(context), target = Unit(context, false);
        int mode = source.AI!.AIMode, events = context.Events.TotalEvents;
        context.Events.Log!.Clear(); Attack(Script(source), target);
        Assert.Same(target, source.AI.Target.ToRead); Assert.Equal(1, source.AI.Unknown10);
        Assert.Equal(mode, source.AI.AIMode); Assert.Equal(events, context.Events.TotalEvents);
        Assert.Empty(context.Events.Log); Assert.Equal(0, context.Random.Draws); Assert.Equal(0, context.Crt.Calls);
        Assert.Equal(1, target.DeletionCount);
        target.Destroy(); Assert.Null(source.AI.Target.ToRead);
    }

    [Fact]
    public void UnitTargetPropagatesEnemySquadBeforeLockingItsOwnTarget()
    {
        ThingContext context = Context(); TestUnit source = Unit(context), target = Unit(context, false);
        NormalSquad sourceSquad = Squad(context), targetSquad = Squad(context);
        source.Squad = sourceSquad; target.Squad = targetSquad;
        source.SetSquadTarget(target);
        Assert.Same(targetSquad, sourceSquad.UnknownC4.ToRead); Assert.Equal(1, sourceSquad.UnknownCC);
        Assert.Same(target, source.AI!.Target.ToRead); Assert.Equal(1, source.AI.Unknown10);
        Assert.Equal(1, targetSquad.DeletionCount);
        targetSquad.Destroy(); Assert.Null(sourceSquad.UnknownC4.ToRead); Assert.Same(target, source.AI.Target.ToRead);
    }

    [Fact]
    public void ExistingWeaponSelectionBoundaryObservesSquadThenAIReaderThenLockOrdering()
    {
        ThingContext context = Context(); TestUnit source = Unit(context), target = Unit(context, false);
        NormalSquad sourceSquad = Squad(context), targetSquad = Squad(context);
        source.Squad = sourceSquad; target.Squad = targetSquad;
        // This deliberately reaches the existing unported spawner-selection guard, after target assignment.
        // It exposes the native order without a new callback or production instrumentation.
        source.UnitData!.Spawners.Add(new UnitWeaponReference(0, 0, 0));
        Assert.Throws<NotSupportedException>(() => source.SetSquadTarget(target));
        Assert.Same(targetSquad, sourceSquad.UnknownC4.ToRead); Assert.Equal(1, sourceSquad.UnknownCC);
        Assert.Same(target, source.AI!.Target.ToRead); Assert.Equal(0, source.AI.Unknown10);
        Assert.Equal(0, context.Random.Draws);
    }

    [Fact]
    public void UnsquaddedUnitTargetClearsSquadReaderButNullDirectTargetLeavesItAlone()
    {
        ThingContext context = Context(); TestUnit source = Unit(context), target = Unit(context, false);
        NormalSquad squad = Squad(context), previous = Squad(context); source.Squad = squad;
        squad.Unknown85(previous); source.SetSquadTarget(target);
        Assert.Null(squad.UnknownC4.ToRead); Assert.Equal(1, squad.UnknownCC); Assert.Equal(0, previous.DeletionCount);
        squad.Unknown85(previous); source.SetSquadTarget(null);
        Assert.Same(previous, squad.UnknownC4.ToRead); Assert.Equal(1, previous.DeletionCount);
        Assert.Null(source.AI!.Target.ToRead); Assert.Equal(1, source.AI.Unknown10); Assert.Equal(0, target.DeletionCount);
    }

    [Fact]
    public void UnitWithoutAIStillSetsItsSquadsTarget()
    {
        ThingContext context = Context(); TestUnit source = Unit(context, false), target = Unit(context, false);
        NormalSquad squad = Squad(context), enemy = Squad(context); source.Squad = squad; target.Squad = enemy;
        source.SetSquadTarget(target); Assert.Same(enemy, squad.UnknownC4.ToRead); Assert.Equal(1, squad.UnknownCC);
        Assert.Equal(0, context.Events.TotalEvents); Assert.Equal(0, context.Random.Draws);
    }

    [Fact]
    public void ComponentBoundaryRefusesBeforePartialTargetMutation()
    {
        ThingContext context = Context(); TestUnit source = Unit(context), target = Unit(context, false);
        NormalSquad squad = Squad(context), previous = Squad(context); source.Squad = squad; squad.Unknown85(previous);
        source.UnitData!.Components.Add(new UnitComponentReference(0, 0));
        Assert.Throws<NotSupportedException>(() => source.SetSquadTarget(target));
        Assert.Same(previous, squad.UnknownC4.ToRead); Assert.Null(source.AI!.Target.ToRead); Assert.Equal(0, source.AI.Unknown10);
        Assert.Equal(0, context.Random.Draws);
    }

    [Fact]
    public void UnitAttackingNormalSquadPicksSmallestFormationOffsetNotFirstOrNearest()
    {
        ThingContext context = Context(); TestUnit source = Unit(context), first = Unit(context, false), selected = Unit(context, false), tie = Unit(context, false);
        NormalSquad target = Squad(context);
        first.Pos = new(1, 0, 0); selected.Pos = new(1000, 0, 0); tie.Pos = new(2, 0, 0);
        target.Members.Append(new NormalSquadMember(first, target) { Unknown08 = 5 });
        target.Members.Append(new NormalSquadMember(selected, target) { Unknown08 = -2 });
        target.Members.Append(new NormalSquadMember(tie, target) { Unknown08 = -2 });
        Attack(Script(source), target); Assert.Same(selected, source.AI!.Target.ToRead); Assert.Equal(1, source.AI.Unknown10);
        selected.Destroy(); Attack(Script(source), target); Assert.Same(tie, source.AI.Target.ToRead);
    }

    [Fact]
    public void EmptyNormalSquadTargetLeavesExistingDirectTargetUnchanged()
    {
        ThingContext context = Context(); TestUnit source = Unit(context), previous = Unit(context, false);
        source.SetSquadTarget(previous); source.AI!.Unknown10 = 0;
        Attack(Script(source), Squad(context)); Assert.Same(previous, source.AI.Target.ToRead); Assert.Equal(0, source.AI.Unknown10);
    }

    [Fact]
    public void RelaxedTargetUsesFirstRemainingMemberAndRelaxedAttackerDoesNothing()
    {
        ThingContext context = Context(); TestUnit source = Unit(context), member = Unit(context, false);
        var target = new RelaxedSquad(context); target.SetThingType(0);
        var empty = new ActiveReader<Thing>(); var read = new ActiveReader<Thing>(); read.SetReader(member);
        target.Members.Append(empty); target.Members.Append(read);
        Attack(Script(source), target); Assert.Same(member, source.AI!.Target.ToRead);
        int events = context.Events.TotalEvents; Attack(Script(target), Squad(context));
        Assert.Equal(events, context.Events.TotalEvents); Assert.Equal(0, context.Random.Draws);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SquadAttackTargetsEnemySquadFromEitherSquadOrMember(bool viaMember)
    {
        ThingContext context = Context(); NormalSquad source = Squad(context), target = Squad(context);
        TestUnit member = Unit(context, false); member.Squad = target;
        Attack(Script(source), viaMember ? member : target);
        Assert.Same(target, source.UnknownC4.ToRead); Assert.Equal(1, source.UnknownCC);
        Assert.Equal(0, context.Events.TotalEvents); Assert.Equal(0, context.Random.Draws); Assert.Equal(0, context.Crt.Calls);
    }

    [Fact]
    public void SquadAttackOnUnsquaddedUnitPreservesItsPriorTarget()
    {
        ThingContext context = Context(); NormalSquad source = Squad(context), old = Squad(context); source.Unknown85(old);
        Attack(Script(source), Unit(context, false)); Assert.Same(old, source.UnknownC4.ToRead);
    }

    [Fact]
    public void NullArgumentWarnsAndUnrelatedTypesAreSilentNoOps()
    {
        ThingContext context = Context(); TestUnit source = Unit(context), target = Unit(context, false); IScript script = Script(source);
        source.SetSquadTarget(target); Attack(script, null);
        Assert.Equal("Warning: Called Attack with NULL object", Assert.Single(script.Runtime.Log));
        Attack(script, new OtherThing(context)); Assert.Same(target, source.AI!.Target.ToRead);
        Attack(Script(new OtherThing(context)), target); Assert.Single(script.Runtime.Log);
        Assert.Equal(0, context.Random.Draws); Assert.Equal(0, context.Crt.Calls);
    }
}
