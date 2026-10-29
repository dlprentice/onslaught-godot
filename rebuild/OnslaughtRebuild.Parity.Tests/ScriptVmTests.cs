// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// The mission-script machine on small hand-made object codes: each case states the retail behaviour it checks
/// (onslaught-decomp src/MissionScript: AsmInstruction.cpp, DataType.h, vm.cpp, IScript.cpp, ScriptEventNB.cpp, MessageBox.cpp).
/// </summary>
public sealed class ScriptVmTests
{
    private sealed class TestThing(ThingContext context) : ComplexThing(context, 0);

    private static ThingContext NewContext()
    {
        var events = new EventManager();
        events.Init();
        var pool = new SPtrSetPool();
        pool.Init(1000);
        return new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), null!, null!);
    }

    private static ScriptValue Int(int v) => new(DataType.Int, v, 0, null, default);

    private static ScriptValue Float(float v) => new(DataType.Float, 0, v, null, default);

    private static ScriptValue Str(string v) => new(DataType.String, 0, 0, Encoding.ASCII.GetBytes(v), default);

    private static Instruction I(Opcode op, int attr = -1) => new(op, attr);

    /// <summary>CALL's attribute: the native's index, its argument count, and whether it returns a value.</summary>
    private static Instruction Call(string native, bool returns = false)
    {
        int index = Array.FindIndex(ScriptNatives.Table, n => n.Name == native);
        return new(Opcode.Call, index | (ScriptNatives.Table[index].NumArgs << 8) | (returns ? 0x10000 : 0));
    }

    /// <summary>An object code whose INIT handler starts at <paramref name="initPc"/>.</summary>
    private static ScriptObjectCode Code(Instruction[] code, ScriptValue?[] symbols, int initPc = 0,
        EventFunction[]? functions = null) => new()
    {
        Instructions = code,
        EventPcs = [initPc, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1],
        Symbols = symbols.Select((v, i) => new Symbol(Encoding.ASCII.GetBytes($"s{i}"), v, 0, i, 1)).ToList(),
        NextSymbolIndex = symbols.Length,
        EventFunctions = functions ?? [],
        Unknown60 = 0,
        Unknown5c = 0,
    };

    private static IScript Start(ThingContext context, ScriptObjectCode code)
    {
        var script = IScript.Create(new TestThing(context), code, context);
        script.Init();
        return script;
    }

    private static ScriptData? Value(IScript script, int symbol) => script.Code!.GetSymbol(symbol).Value;

    [Fact]
    public void ArithmeticTakesTheLeftOperandsKind()
    {
        // PLUS asks the lower value (a->Add(b)): Int 7 + Float 0.5 is an
        // int add of GetInt (a float-to-integer store to 0.5 = 0, to even);
        // Float 0.5 + Int 7 is a float add.
        ThingContext context = NewContext();
        IScript script = Start(context, Code(
            [I(Opcode.Push, 0), I(Opcode.Push, 1), I(Opcode.Plus), I(Opcode.Pop, 2),
             I(Opcode.Push, 1), I(Opcode.Push, 0), I(Opcode.Plus), I(Opcode.Pop, 3), I(Opcode.Return)],
            [Int(7), Float(0.5f), null, null]));

        Assert.Equal(7, Assert.IsType<IntData>(Value(script, 2)).Value);
        Assert.Equal(7.5f, Assert.IsType<FloatData>(Value(script, 3)).Value);
        Assert.Empty(ScriptRuntime.Of(context).Log);
    }

    [Fact]
    public void IntGetFloatIsExactSoAFloatOperationRoundsOnce()
    {
        // CIntDataType::GetFloat is a bare fild (0052d370): 1.0f + 16777217 rounds once to 16777218; rounding the int to
        // a float first would give 16777216.
        ThingContext context = NewContext();
        IScript script = Start(context, Code(
            [I(Opcode.Push, 0), I(Opcode.Push, 1), I(Opcode.Plus), I(Opcode.Pop, 2), I(Opcode.Return)],
            [Float(1.0f), Int(16777217), null]));

        Assert.Equal(16777218.0f, Assert.IsType<FloatData>(Value(script, 2)).Value);
    }

    [Fact]
    public void CmpbAsksTheTopValue()
    {
        // CMPB pops a (the top) then b and asks a->Equals(b): Float 3.4 == Int 3 compares 3.4 with 3.0 (false); Int 3 ==
        // Float 3.4 compares 3 with fistp(3.4) = 3 (true).
        ThingContext context = NewContext();
        IScript script = Start(context, Code(
            [I(Opcode.Push, 0), I(Opcode.Push, 1), I(Opcode.CmpB), I(Opcode.Pop, 2),
             I(Opcode.Push, 1), I(Opcode.Push, 0), I(Opcode.CmpB), I(Opcode.Pop, 3), I(Opcode.Return)],
            [Int(3), Float(3.4f), null, null]));

        Assert.False(Value(script, 2)!.IsTrue());
        Assert.True(Value(script, 3)!.IsTrue());
    }

    [Fact]
    public void AFloatZeroIsTrue()
    {
        // CFloatDataType has no IsTrue: CDataType's returns true, so JMPFALSE never jumps on a float.
        ThingContext context = NewContext();
        IScript script = Start(context, Code(
            [I(Opcode.Push, 0), I(Opcode.JmpFalse, 4), I(Opcode.Push, 1), I(Opcode.Pop, 2), I(Opcode.Return)],
            [Float(0.0f), Int(1), null]));

        Assert.Equal(1, Value(script, 2)!.GetInt());
    }

    [Fact]
    public void LocalCallsReturnAndPushOne()
    {
        // PUSHPC pushes the return address, CALLLOCAL jumps and counts the depth, RETURN at depth 1 pops the address and
        // pushes Int 1; Run ends only at a RETURN outside any local call.
        ThingContext context = NewContext();
        IScript script = Start(context, Code(
            [I(Opcode.PushPc, 3), I(Opcode.CallLocal, 5), I(Opcode.Nop), I(Opcode.Pop, 0), I(Opcode.Return),
             I(Opcode.Push, 1), I(Opcode.Pop, 2), I(Opcode.Return)],
            [null, Int(42), null]));

        Assert.Equal(42, Value(script, 2)!.GetInt());
        Assert.Equal(1, Value(script, 0)!.GetInt());
        Assert.Empty(ScriptRuntime.Of(context).Log);
    }

    [Fact]
    public void PauseSuspendsAndResumesAtTheScheduledTick()
    {
        // Pause saves a copy of the machine and stops it; CALL then pushes its Int 1 onto the saved copy's stack (the
        // script's newest saved machine) and the running stack is emptied. RESUME is due at now + 1.0 = buffer
        // floor((1.0 - 0.001) * 20) = 19, flushed by the 20th update.
        ThingContext context = NewContext();
        IScript script = Start(context, Code(
            [I(Opcode.Push, 0), Call("Pause"), I(Opcode.RemoveTop), I(Opcode.Push, 1), I(Opcode.Pop, 2), I(Opcode.Return)],
            [Float(1.0f), Int(9), null]));
        ScriptRuntime runtime = ScriptRuntime.Of(context);

        Assert.Null(Value(script, 2));
        Assert.Equal(1, script.SavedVms.Size);
        Assert.Equal(1, script.SavedVms.First()!.Stack.Count);
        Assert.Equal(0, runtime.Vm.Stack.Count);
        Assert.False(runtime.Vm.Running);

        int resumedAt = -1;
        for (int frame = 1; frame <= 40 && resumedAt < 0; frame++)
        {
            context.Events.Update();
            if (Value(script, 2) is not null)
            {
                resumedAt = context.Events.FrameCount;
            }
        }

        Assert.Equal(20, resumedAt);
        Assert.Equal(9, Value(script, 2)!.GetInt());
        Assert.Equal(0, script.SavedVms.Size);
        Assert.Empty(runtime.Log);
    }

    [Fact]
    public void APostedEventRunsItsFunctionNextFrameWithTheFiredFlags()
    {
        // PostEvent schedules SCRIPT_EVENT_NB_POST for the next frame; the event function (registered by Init after the
        // init event) runs with one bool per listened-for name: whether that name has ever fired.
        ThingContext context = NewContext();
        ScriptRuntime runtime = ScriptRuntime.Of(context);
        runtime.EventNB.Init();
        IScript script = Start(context, Code(
            [I(Opcode.Push, 0), Call("PostEvent"), I(Opcode.RemoveTop), I(Opcode.Return),
             I(Opcode.Pop, 2), I(Opcode.Pop, 3), I(Opcode.Return)],
            [Str("go"), Str("never"), null, null],
            functions: [new EventFunction(4, [1, 0])]));

        Assert.Null(Value(script, 2));
        context.Events.Update();

        // Arguments are pushed in parameter order ("never", then "go"), so the first POP takes "go"'s flag.
        Assert.True(Value(script, 2)!.IsTrue());
        Assert.False(Value(script, 3)!.IsTrue());
        Assert.Empty(runtime.Log);
    }

    [Fact]
    public void ACloneBindsEventNamesToItsOwnSymbolValues()
    {
        // CEventFunction::Clone finds the event name's symbol position in the world's table and takes the copy's
        // symbol value itself (not a clone of it).
        ThingContext context = NewContext();
        ScriptObjectCode stored = Code([I(Opcode.Return)], [Str("a"), Str("b")], functions: [new EventFunction(0, [1])]);
        ScriptCode copy = ScriptCode.Clone(stored, context.Pool);

        EventParam param = copy.EventFunctions!.First()!.Params.First()!;
        Assert.Same(copy.Symbols[1].Value, param.Name);
    }

    private sealed class FixedText(Dictionary<uint, string> strings) : IGameText
    {
        public string? GetString(uint id) => strings.GetValueOrDefault(id);

        public string? GetSoundName(uint id) => null;

        public string LanguageName => "english";
    }

    private sealed class NoVoice : IMessageVoice
    {
        public bool SoundInitialised => false;

        public void StartLoad(string path) => throw new InvalidOperationException();

        public bool LoaderBusy => false;

        public bool HasLoadedData => false;

        public void PlayLoaded(ScriptMessage? message) => throw new InvalidOperationException();

        public void KillVoice()
        {
        }

        public void UpdateStatus()
        {
        }
    }

    [Fact]
    public void PlayCharMessageWaitResumesWhenTheBoxHasShownTheMessage()
    {
        // A 22-letter message without a voice: duration = 22 * (1/22) + 0.2 = 1.2 (rounded each step). The box may play
        // from update 1 (ALLOWED_TO_PLAY_MESSAGES); the message starts 0.2 s later, reveals each tick while
        // (1.2 + start) + 0.2 > now, finishes 0.3 s after that, and its event (RESUME, 0.05 s) wakes the script.
        ThingContext context = NewContext();
        ScriptRuntime runtime = ScriptRuntime.Of(context);
        runtime.Text = new FixedText(new() { [1] = "Speaker", [2] = new string('x', 22) });
        var box = new MessageBox(context, runtime.Text, new NoVoice());
        runtime.MessageBox = box;
        IScript script = Start(context, Code(
            [I(Opcode.Push, 0), I(Opcode.Push, 1), I(Opcode.Push, 2), Call("PlayCharMessageWait"), I(Opcode.RemoveTop),
             I(Opcode.Push, 3), I(Opcode.Pop, 4), I(Opcode.Return)],
            [Int(1), Int(2), Float(0.0f), Int(5), null]));
        context.Events.AddEvent(MessageBox.AllowedToPlayMessages, box, EventManager.NextFrame);

        Assert.Equal(1, box.Queue.Size);
        Assert.Equal(1.2f, box.Queue.First()!.Duration);

        // The expected tick, from the retail rules on the event manager's 24-bit clock.
        int expected = ExpectedResume(1.2f);
        int resumedAt = -1;
        for (int frame = 1; frame <= 200 && resumedAt < 0; frame++)
        {
            context.Events.Update();
            if (Value(script, 4) is not null)
            {
                resumedAt = context.Events.FrameCount;
            }
        }

        Assert.Equal(expected, resumedAt);
        Assert.Single(box.Shown);
        Assert.Equal(0, box.Queue.Size);
    }

    /// <summary>
    /// The resume tick for one voiceless message allowed at update 1: the time of update f is f * 0.05 (fild, fmul); an
    /// event due within 0.051 s runs next update, a later one at update f + floor((due - now - 0.001) * 20) + 1.
    /// </summary>
    private static int ExpectedResume(float duration)
    {
        static float T(int f) => (float)Numerics.X87.Mul(f, 0.05f);
        static int Due(int f, float delay)
        {
            float due = T(f) + delay;
            return due <= T(f) + 0.051f ? f + 1 : f + (int)Math.Floor((due - T(f) - 0.001f) * 20.0f) + 1;
        }

        int start = 1;
        float startTime = T(start);
        int f = Due(start, 0.2f);
        while ((duration + startTime) + 0.2f > T(f))
        {
            f = Due(f, 0.038f);
        }

        int finish = Due(f, 0.3f);
        return Due(finish, 0.05f);
    }
}
