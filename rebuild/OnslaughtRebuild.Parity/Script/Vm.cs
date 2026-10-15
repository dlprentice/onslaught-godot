// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/MissionScript/vm.cpp (CVM 005398d0, operator= 00539910, Clean 00539980, CallEvent
// 00539990, CallFunction 00539a60, GotoInstruction 00539ae0, Run 00539b00) and ScriptObjectCode.cpp (CDataStack:
// operator= 00539350, Clear 005393e0, Push 00539420, Pop 00539470, RemoveTop 005394a0, GetTop 005394e0), reconstructed
// from the retail executable; not in Stuart's release.

using System.Runtime.CompilerServices;
using OnslaughtRebuild.Parity.Events;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Script;

/// <summary>CDataStack: up to 128 values; a copy shares its values (the implicit copy constructor).</summary>
public sealed class DataStack
{
    /// <summary>MAX_STACK_SIZE.</summary>
    public const int MaxSize = 128;

    private readonly ScriptRuntime _runtime;

    public DataStack(ScriptRuntime runtime) => _runtime = runtime;

    public DataStack(DataStack from)
    {
        _runtime = from._runtime;
        Array.Copy(from.Values, Values, from.Count);
        Count = from.Count;
    }

    public ScriptData?[] Values { get; } = new ScriptData?[MaxSize];

    /// <summary>mNumValues.</summary>
    public int Count { get; set; }

    /// <summary>operator=: takes the other stack's values, leaving it empty (its own are deleted first).</summary>
    public void TakeFrom(DataStack from)
    {
        Clear();
        Count = from.Count;
        Array.Copy(from.Values, Values, Count);
        from.Count = 0;
    }

    /// <summary>Clear: deletes the values from the top down.</summary>
    public void Clear()
    {
        while (Count != 0)
        {
            Values[Count - 1]?.Delete();
            Count--;
        }
    }

    /// <summary>Push. Retail's 129th push writes over the count itself; that is not modelled.</summary>
    public void Push(ScriptData? value)
    {
        if (Count == MaxSize)
        {
            throw new InvalidOperationException("FATAL ERROR: Stack out of memory (retail corrupts the stack count)");
        }

        Values[Count++] = value;
    }

    /// <summary>Pop: null (after a log line) when empty.</summary>
    public ScriptData? Pop()
    {
        if (Count == 0)
        {
            _runtime.LogMessage("FATAL ERROR: Pop called on empty stack");
            return null;
        }

        return Values[--Count];
    }

    public void RemoveTop()
    {
        if (Count == 0)
        {
            _runtime.LogMessage("FATAL ERROR: RemoveTop called on empty stack");
            return;
        }

        Values[--Count]?.Delete();
    }

    /// <summary>GetTop: the value <paramref name="depth"/> below the top, or null (after a log line).</summary>
    public ScriptData? GetTop(int depth)
    {
        if (Count - depth <= 0)
        {
            _runtime.LogMessage($"FATAL ERROR: Stack item does not exist in call to GetTop - {depth}");
            return null;
        }

        return Values[Count - depth - 1];
    }
}

/// <summary>
/// CVM: the one mission-script machine every script shares (MISSION_SCRIPT_VM). It runs one script at a time from a
/// program counter to the return; a Wait native keeps a copy of it (the implicit copy constructor: the stack's values
/// are shared, and the running machine's count is zeroed when it stops) and assignment later restores it.
/// </summary>
public sealed partial class ScriptVm : Monitor
{
    /// <summary>MAX_INSTRUCTIONS_PER_RUN.</summary>
    public const int MaxInstructionsPerRun = 10000;

    private readonly ScriptRuntime _runtime;

    public ScriptVm(ScriptRuntime runtime)
    {
        _runtime = runtime;
        Stack = new DataStack(runtime);
    }

    /// <summary>The implicit copy constructor (the Wait natives' saved machines).</summary>
    public ScriptVm(ScriptVm from)
    {
        _runtime = from._runtime;
        Code = from.Code;
        Stack = new DataStack(from.Stack);
        Running = from.Running;
        Pc = from.Pc;
        Flags = from.Flags;
        StackBase = from.StackBase;
        Stop = from.Stop;
        CallDepth = from.CallDepth;
    }

    /// <summary>mCode.</summary>
    public ScriptCode? Code { get; set; }

    /// <summary>mStack.</summary>
    public DataStack Stack { get; }

    /// <summary>mRunning.</summary>
    public bool Running { get; set; }

    /// <summary>mPC: during Execute it already points past the instruction.</summary>
    public int Pc { get; set; }

    /// <summary>mFlags: bit 0 is the last CMP.</summary>
    public uint Flags { get; set; }

    /// <summary>mStackBase: the stack's size when the run began; a finished run should end there.</summary>
    public int StackBase { get; set; }

    /// <summary>mStop: a Wait native suspended the run, or the run hit the instruction limit.</summary>
    public bool Stop { get; set; }

    /// <summary>mCallDepth: local calls (CALLLOCAL) not yet returned.</summary>
    public int CallDepth { get; set; }

    /// <summary>operator=: back to a saved machine's state; the stack's values move across; not running.</summary>
    public void Restore(ScriptVm other)
    {
        Code = other.Code;
        Pc = other.Pc;
        Stack.TakeFrom(other.Stack);
        Flags = other.Flags;
        Stop = other.Stop;
        StackBase = other.StackBase;
        CallDepth = other.CallDepth;
        Running = false;
    }

    /// <summary>Clean: the stack's values are deleted.</summary>
    public void Clean() => Stack.Clear();

    /// <summary>CallEvent: the handler for an event, if the code has one, with its arguments on the stack.</summary>
    public void CallEvent(ScriptCode code, int scriptEvent, IReadOnlyList<ScriptData> args)
    {
        if (Stack.Count != 0)
        {
            _runtime.LogMessage("FATAL ERROR: stack not empty on call");
        }

        Code = code;
        RunInitCodeOnce(code);

        Pc = Code.EventPcs[scriptEvent];
        if (Pc == -1)
        {
            foreach (ScriptData a in args)
            {
                a.Delete();
            }

            return;
        }

        StackBase = Stack.Count;
        foreach (ScriptData a in args)
        {
            Stack.Push(a);
        }

        Run();
    }

    /// <summary>CallFunction: an event function's code from its entry, with its arguments on the stack.</summary>
    public void CallFunction(ScriptCode code, int pc, IReadOnlyList<ScriptData> args)
    {
        Code = code;
        RunInitCodeOnce(code);

        StackBase = Stack.Count;
        foreach (ScriptData a in args)
        {
            Stack.Push(a);
        }

        Pc = pc;
        Run();
    }

    private void RunInitCodeOnce(ScriptCode code)
    {
        if (!code.InitRun && code.Unknown5c != 0)
        {
            Pc = 0;
            StackBase = Stack.Count;
            Run();
            code.InitRun = true;
        }
    }

    /// <summary>GotoInstruction: a resumed machine carries on from <paramref name="pc"/>.</summary>
    public void GotoInstruction(int pc)
    {
        Pc = pc;
        Run();
    }

    /// <summary>
    /// Run: until a RETURN outside any local call, or until something stops the machine. A stopped run leaves its values
    /// with the saved copy (the count is zeroed); a finished one logs a stack of the wrong size and empties it.
    /// </summary>
    public void Run()
    {
        if (Running)
        {
            _runtime.LogMessage("ERROR: VM tryin to run VM whilst it was already running.");
            return;
        }

        Running = true;
        Flags = 0;
        CallDepth = 0;
        Stop = false;

        // The instructions are read through mCode each time; the symbol table is the one the run began with.
        Instruction instruction = Code!.Instructions[Pc];
        ScriptCode symbols = Code!;
        int count = 0;
        while (!(instruction.Opcode == Opcode.Return && CallDepth <= 0) && !Stop)
        {
            int pc = Pc++;
            _runtime.Trace?.Invoke(Code!, pc, instruction);
            Execute(instruction, symbols);
            if (Code!.Unknown60 == 1)
            {
                _runtime.LogMessage($"-> {pc,4} stack size = {Stack.Count} flags = {Flags}");
            }

            instruction = Code.Instructions[Pc];
            if (++count > MaxInstructionsPerRun)
            {
                _runtime.LogMessage("FATAL ERROR :Infinite loop in script!");
                Stop = true;
            }
        }

        Running = false;
        if (Stop)
        {
            Stack.Count = 0;
            return;
        }

        if (Stack.Count != StackBase)
        {
            _runtime.LogMessage("FATAL ERROR:  stack was different size when exiting");
        }

        while (Stack.Count != 0)
        {
            Stack.RemoveTop();
        }
    }
}

/// <summary>
/// The mission-script globals of one simulation: MISSION_SCRIPT_VM, SCRIPT_EVENT_NB, gEventArg, gScriptFunctionArgs and
/// the debug log. One per <see cref="ThingContext"/>, so separate simulations never share them.
/// </summary>
public sealed class ScriptRuntime
{
    private static readonly ConditionalWeakTable<ThingContext, ScriptRuntime> Runtimes = new();

    private ScriptRuntime(ThingContext context)
    {
        Context = context;
        Vm = new ScriptVm(this);
        EventNB = new ScriptEventNB(this);
    }

    /// <summary>The runtime of a context, made on first use.</summary>
    public static ScriptRuntime Of(ThingContext context) => Runtimes.GetValue(context, c => new ScriptRuntime(c));

    public ThingContext Context { get; }

    /// <summary>MISSION_SCRIPT_VM.</summary>
    public ScriptVm Vm { get; }

    /// <summary>SCRIPT_EVENT_NB.</summary>
    public ScriptEventNB EventNB { get; }

    /// <summary>gEventArg: the argument Arrived and Hit made last (the other notifications pass a count of 0).</summary>
    public ScriptData? EventArg { get; set; }

    /// <summary>gScriptFunctionArgs: a native call's popped arguments.</summary>
    public ScriptData?[] FunctionArgs { get; } = new ScriptData?[7];

    /// <summary>The in-game message box (CGame's mMessageBox), when one is made.</summary>
    public MessageBox? MessageBox { get; set; }

    /// <summary>TEXT, for the message natives and the message box.</summary>
    public IGameText? Text { get; set; }

    /// <summary>
    /// GAME.GetPlayer(n): whether player n exists and its battle engine (CPlayer::GetBattleEngine). Set by the world
    /// wiring; null means the players are not wired, and GetPlayer throws.
    /// </summary>
    public Func<int, (bool HasPlayer, Thing? BattleEngine)>? Player { get; set; }

    /// <summary>CWaypointManager's paths (the loader's), for the FollowWaypoint natives.</summary>
    public WaypointManager? Waypoints { get; set; }

    /// <summary>Retail's LOG.AddMessage lines from the script system, in order.</summary>
    public List<string> Log { get; } = [];

    /// <summary>Called before each executed instruction: (code, pc, instruction). The oracle trace hook.</summary>
    public Action<ScriptCode, int, Instruction>? Trace { get; set; }

    public void LogMessage(string message) => Log.Add(message);
}
