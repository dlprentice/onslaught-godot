// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/MissionScript/ScriptObjectCode.cpp (the file constructor 00538ec0), AsmInstruction.cpp
// (CAsmInstruction::Spawn 0052d3d0), Symtab.cpp (CSymbolTable 00539770, CSymbol), DataType.cpp (CDataType::Create
// 0052ec60, CStringDataType) and EventFunction.cpp (CEventFunction 0052fa70). Not in Stuart's release.

using System.Text;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Script;

/// <summary>EOpcode: the VM's instructions, numbered as the object code stores them.</summary>
public enum Opcode
{
    Nop,
    Plus,
    Minus,
    Multiply,
    Divide,
    Push,
    Pop,
    Or,
    And,
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual,
    Label,
    RemoveTop,
    Cmp,
    CmpB,
    CmpNeB,
    JmpNe,
    JmpFalse,
    Jmp,
    GetTop,
    Pointer,
    Return,
    Call,
    CallLocal,
    PushPc,
}

/// <summary>EDataType.</summary>
public enum DataType
{
    None,
    Int,
    Float,
    String,
    Bool,
    ThingPtr,
    Position,
}

/// <summary>One instruction: its opcode and the attribute stored after it.</summary>
public readonly record struct Instruction(Opcode Opcode, int Attribute);

/// <summary>A symbol-table value as stored (CDataType::Create); a thing pointer keeps only its stored id.</summary>
public sealed record ScriptValue(DataType Type, int Int, float Float, byte[]? String, FVector Position)
{
    public static ScriptValue Read(DataType type, MemBuffer file) => type switch
    {
        DataType.Int or DataType.Bool or DataType.ThingPtr => new ScriptValue(type, file.ReadInt32(), 0, null, default),
        DataType.Float => new ScriptValue(type, 0, file.ReadSingle(), null, default),
        DataType.String => new ScriptValue(type, 0, 0, ReadString(file), default),
        DataType.Position => new ScriptValue(type, 0, 0, null, new FVector(file.ReadSingle(), file.ReadSingle(), file.ReadSingle())),
        _ => throw new InvalidDataException($"unknown data type to spawn ({(int)type})"),
    };

    /// <summary>CStringDataType's file constructor: a 32-bit length, then the bytes.</summary>
    public static byte[] ReadString(MemBuffer file)
    {
        int length = file.ReadInt32();
        if (length < 0)
        {
            throw new InvalidDataException($"{file.FileName}: negative script string length.");
        }

        var text = new byte[length];
        return file.Read(text) == length ? text : throw new EndOfStreamException(file.FileName);
    }

    public override string ToString() => Type switch
    {
        DataType.Float => Float.ToString("R"),
        DataType.String => '"' + Encoding.ASCII.GetString(String!) + '"',
        DataType.Position => Position.ToString(),
        _ => $"{Type} {Int}",
    };
}

/// <summary>CSymbol: a named variable, its initial value (null when none), the compiler's type, its index and a flag.</summary>
public sealed record Symbol(byte[] Name, ScriptValue? Value, int Type, int Index, int Flag)
{
    public string NameText => Encoding.ASCII.GetString(Name);
}

/// <summary>CEventFunction: an entry point and the symbol indices of the event names it waits for.</summary>
public sealed record EventFunction(int EntryPc, IReadOnlyList<int> ParameterSymbols);

/// <summary>A compiled mission script (CMissionScriptObjectCode) as the world file stores it.</summary>
public sealed class ScriptObjectCode
{
    /// <summary>NUM_SCRIPT_EVENTS.</summary>
    public const int EventCount = 13;

    public required IReadOnlyList<Instruction> Instructions { get; init; }

    /// <summary>mEventPCs: the handler's instruction index per event, -1 for none.</summary>
    public required int[] EventPcs { get; init; }

    public required IReadOnlyList<Symbol> Symbols { get; init; }

    /// <summary>mNextIndex of the symbol table.</summary>
    public required int NextSymbolIndex { get; init; }

    public required IReadOnlyList<EventFunction> EventFunctions { get; init; }

    public required int Unknown60 { get; init; }

    public required int Unknown5c { get; init; }

    public static ScriptObjectCode Read(MemBuffer file)
    {
        int count = file.ReadInt32();
        var instructions = new List<Instruction>(count);
        for (int i = 0; i < count; i++)
        {
            int opcode = file.ReadInt32();
            int attribute = file.ReadInt32();
            if (opcode < 0 || opcode > (int)Opcode.PushPc)
            {
                throw new InvalidDataException($"unknown instruction {opcode} in spawn");
            }

            instructions.Add(new Instruction((Opcode)opcode, attribute));
        }

        var eventPcs = new int[EventCount];
        for (int i = 0; i < EventCount; i++)
        {
            eventPcs[i] = file.ReadInt32();
        }

        int symbolCount = file.ReadInt32();
        var symbols = new List<Symbol>(symbolCount);
        for (int i = 0; i < symbolCount; i++)
        {
            byte[] name = ScriptValue.ReadString(file);
            int type = file.ReadInt32();
            ScriptValue? value = type == 0 ? null : ScriptValue.Read((DataType)type, file);
            symbols.Add(new Symbol(name, value, file.ReadInt32(), file.ReadInt32(), file.ReadInt32()));
        }

        int nextIndex = file.ReadInt32();
        int functionCount = file.ReadInt32();
        var functions = new List<EventFunction>(functionCount);
        for (int i = 0; i < functionCount; i++)
        {
            int entry = file.ReadInt32();
            int parameterCount = file.ReadInt32();
            var parameters = new int[parameterCount];
            for (int p = 0; p < parameterCount; p++)
            {
                parameters[p] = file.ReadInt32();
            }

            functions.Add(new EventFunction(entry, parameters));
        }

        return new ScriptObjectCode
        {
            Instructions = instructions,
            EventPcs = eventPcs,
            Symbols = symbols,
            NextSymbolIndex = nextIndex,
            EventFunctions = functions,
            Unknown60 = file.ReadInt32(),
            Unknown5c = file.ReadInt32(),
        };
    }
}
