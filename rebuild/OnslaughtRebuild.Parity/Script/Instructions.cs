// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/MissionScript/AsmInstruction.cpp (CAsmInstruction::GetAttributeValue 0052d040 and each
// instruction's Execute: PUSHPC 0052e0a0, RETURN 0052e0f0, PLUS 0052e180, MINUS 0052e1d0, MULTIPLY 0052e220, DIVIDE
// 0052e270, PUSH 0052e2c0, POP 0052e2f0, REMOVE_TOP 0052e320, CMP 0052e330, CMPB 0052e380, OR 0052e4d0, AND 0052e580,
// GREAT_THAN 0052e630, LESS_THAN 0052e6d0, GREAT_EQ_THAN 0052e770, LESS_EQ_THAN 0052e810, CMPNEB 0052e8b0, JMPFALSE
// 0052e950, JMPNE 0052e990, JMP 0052e9b0, GETTOP 0052e9c0, POINTER 0052ea10, CALL 0052ea40, CALLLOCAL 0052ec40; NOP and
// LABEL do nothing), reconstructed from the retail executable; not in Stuart's release.

namespace OnslaughtRebuild.Parity.Script;

public sealed partial class ScriptVm
{
    /// <summary>One instruction, with the program counter already past it and the run's symbol table.</summary>
    private void Execute(Instruction instruction, ScriptCode symbols)
    {
        int attr = instruction.Attribute;
        switch (instruction.Opcode)
        {
            case Opcode.Nop:
            case Opcode.Label:
                break;

            case Opcode.PushPc:
                Stack.Push(new IntData(attr));
                break;

            case Opcode.Return:
                if (CallDepth > 0)
                {
                    CallDepth--;
                    ScriptData address = Stack.Pop()!;
                    Pc = address.GetInt();
                    address.Delete();
                    Stack.Push(new IntData(1));
                }

                break;

            case Opcode.Plus:
                Arithmetic((a, b) => a.Add(b));
                break;
            case Opcode.Minus:
                Arithmetic((a, b) => a.Subtract(b));
                break;
            case Opcode.Multiply:
                Arithmetic((a, b) => a.Multiply(b));
                break;
            case Opcode.Divide:
                Arithmetic((a, b) => a.Divide(b));
                break;

            case Opcode.Push:
                Stack.Push(GetAttributeValue(symbols.GetSymbol(attr)));
                break;

            case Opcode.Pop:
            {
                ScriptSymbol symbol = symbols.GetSymbol(attr);
                symbol.Value?.Delete();
                symbol.Value = Stack.Pop();
                break;
            }

            case Opcode.RemoveTop:
                Stack.RemoveTop();
                break;

            case Opcode.Cmp:
                if (Stack.GetTop(0)!.IsEqual(Stack.GetTop(1)!))
                {
                    Flags |= 1;
                }
                else
                {
                    Flags &= ~1u;
                }

                break;

            // CMPB, CMPNEB, OR and AND ask the top value about the one below it; the orderings ask the lower value.
            case Opcode.CmpB:
                TopFirst((a, b) => a.IsEqual(b));
                break;
            case Opcode.CmpNeB:
                TopFirst((a, b) => a.NotEquals(b));
                break;
            case Opcode.Or:
                TopFirst((a, b) => a.IsTrue() || b.IsTrue());
                break;
            case Opcode.And:
                TopFirst((a, b) => a.IsTrue() && b.IsTrue());
                break;

            case Opcode.GreaterThan:
                Ordering((a, b) => a.GreaterThan(b));
                break;
            case Opcode.LessThan:
                Ordering((a, b) => a.LessThan(b));
                break;
            case Opcode.GreaterOrEqual:
                Ordering((a, b) => a.GreaterOrEqual(b));
                break;
            case Opcode.LessOrEqual:
                Ordering((a, b) => a.LessOrEqual(b));
                break;

            case Opcode.JmpFalse:
            {
                ScriptData value = Stack.Pop()!;
                if (!value.IsTrue())
                {
                    Pc = attr;
                }

                value.Delete();
                break;
            }

            case Opcode.JmpNe:
                if ((Flags & 1) == 0)
                {
                    Pc = attr;
                }

                break;

            case Opcode.Jmp:
                Pc = attr;
                break;

            case Opcode.GetTop:
            {
                ScriptSymbol symbol = symbols.GetSymbol(attr);
                ScriptData top = Stack.GetTop(0)!;
                if (symbol.Value is null)
                {
                    symbol.Value = top.Clone();
                }
                else
                {
                    symbol.Value.Assign(top);
                }

                break;
            }

            case Opcode.Pointer:
            {
                ScriptData value = Stack.Pop()!;
                Code!.Script!.Thing = value.GetThing();
                value.Delete();
                break;
            }

            case Opcode.Call:
                Call(attr);
                break;

            case Opcode.CallLocal:
                Pc = attr;
                CallDepth++;
                break;

            default:
                throw new InvalidOperationException($"unknown opcode {instruction.Opcode}");
        }
    }

    /// <summary>GetAttributeValue: a clone of the symbol's value, or Int 0 (after a log line) when it has none.</summary>
    private ScriptData GetAttributeValue(ScriptSymbol symbol)
    {
        if (symbol.Value is not null)
        {
            return symbol.Value.Clone();
        }

        _runtime.LogMessage($"FATAL ERROR:  no data set for attribute '{System.Text.Encoding.Latin1.GetString(symbol.Name)}'");
        return new IntData(0);
    }

    /// <summary>PLUS..DIVIDE: b is popped first; the result is a's (null where a's kind has no such operator).</summary>
    private void Arithmetic(Func<ScriptData, ScriptData, ScriptData?> op)
    {
        ScriptData b = Stack.Pop()!;
        ScriptData a = Stack.Pop()!;
        ScriptData? result = op(a, b);
        b.Delete();
        a.Delete();
        Stack.Push(result);
    }

    /// <summary>CMPB, CMPNEB, OR, AND: a is the top value; the bool is pushed before both are deleted.</summary>
    private void TopFirst(Func<ScriptData, ScriptData, bool> test)
    {
        ScriptData a = Stack.Pop()!;
        ScriptData b = Stack.Pop()!;
        Stack.Push(new BoolData(test(a, b)));
        a.Delete();
        b.Delete();
    }

    /// <summary>GREAT_THAN..LESS_EQ_THAN: b is popped first, a (below it) is asked.</summary>
    private void Ordering(Func<ScriptData, ScriptData, bool> test)
    {
        ScriptData b = Stack.Pop()!;
        ScriptData a = Stack.Pop()!;
        Stack.Push(new BoolData(test(a, b)));
        b.Delete();
        a.Delete();
    }

    /// <summary>
    /// CALL: a native. The attribute's low byte picks it, the next byte counts its arguments (popped last first into
    /// gScriptFunctionArgs), and a positive high half says it returns a value. With no thing to act on it pushes Int 0.
    /// After the call the thing goes back to the script's own; if the native stopped the machine, the value goes onto
    /// the stack of the script's newest saved machine (IScript +0x2c: its saved set's last node), which resumes with it.
    /// </summary>
    private void Call(int attr)
    {
        IScript script = Code!.Script!;
        int numArgs = (attr >> 8) & 0xff;
        ScriptData?[] args = _runtime.FunctionArgs;
        for (int c0 = numArgs; c0 > 0; c0--)
        {
            args[c0 - 1] = Stack.Pop();
        }

        ScriptNative native = ScriptNatives.Table[attr & 0xff];
        if (script.Thing is null)
        {
            _runtime.LogMessage($"FATAL ERROR: Can't call function '{native.Name}' on NULL object");
            Stack.Push(new IntData(0));
            script.Thing = script.DefaultThing;
            for (int c0 = 0; c0 < numArgs; c0++)
            {
                args[c0]?.Delete();
            }

            return;
        }

        ScriptData? result = null;
        native.Function(script, args, numArgs, ref result);

        for (int c0 = 0; c0 < numArgs; c0++)
        {
            args[c0]?.Delete();
        }

        DataStack results = Stack;
        if (Stop)
        {
            results = (script.SavedVms.Last() ?? throw new InvalidOperationException("a stopped call with no saved machine")).Stack;
        }

        if ((attr & ~0xffff) > 0)
        {
            if (result is null)
            {
                _runtime.LogMessage($"ERROR: function '{native.Name}' was expecting return value!");
            }
            else
            {
                results.Push(result);
            }
        }
        else
        {
            if (result is not null)
            {
                _runtime.LogMessage($"ERROR: script function '{native.Name}' returned unexpected value");
            }

            results.Push(new IntData(1));
        }

        script.Thing = script.DefaultThing;
    }
}
