// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/MissionScript/ScriptObjectCode.cpp (CMissionScriptObjectCode::Clone 00539040,
// ~CMissionScriptObjectCode 005391a0), Symtab.cpp (CSymbol::Clone, CSymbolTable::Find, Add, ~CSymbolTable 00539510,
// Clone 005395b0, GetSymbol 00539760) and EventFunction.cpp (the file constructor 0052fa70, Clone 0052fbb0), reconstructed
// from the retail executable; not in Stuart's release.

using OnslaughtRebuild.Parity.Containers;

namespace OnslaughtRebuild.Parity.Script;

/// <summary>CSymbol at run time: the name, the current value (null until set), the compiler's type and the index.</summary>
public sealed class ScriptSymbol(byte[] name, ScriptData? value, int type)
{
    public byte[] Name { get; } = name;

    public ScriptData? Value { get; set; } = value;

    public int Type { get; } = type;

    public int Index { get; set; }

    public override string ToString() => $"{System.Text.Encoding.Latin1.GetString(Name)}={Value}";
}

/// <summary>
/// CMissionScriptObjectCode as a thing runs it: a copy of the world's code made by Clone, with its own symbols and event
/// functions. The instructions are shared with the stored code (retail clones each one; they never change).
/// </summary>
public sealed class ScriptCode
{
    private ScriptCode(ScriptObjectCode stored, int[] eventPcs)
    {
        Stored = stored;
        EventPcs = eventPcs;
    }

    public ScriptObjectCode Stored { get; }

    public IReadOnlyList<Instruction> Instructions => Stored.Instructions;

    /// <summary>mEventPCs.</summary>
    public int[] EventPcs { get; }

    /// <summary>mSymbols, by position (GetSymbol is at(n)).</summary>
    public List<ScriptSymbol> Symbols { get; } = [];

    /// <summary>The symbol table's mNextIndex.</summary>
    public int NextSymbolIndex { get; private set; }

    /// <summary>mEventFunctions.</summary>
    public SPtrSet<ScriptEventFunction>? EventFunctions { get; private set; }

    /// <summary>mUnknown5c: CVM runs the init code (from pc 0) once, before the first event, when it is set.</summary>
    public int Unknown5c { get; private set; }

    /// <summary>mUnknown60: 1 makes CVM::Run log each instruction.</summary>
    public int Unknown60 { get; private set; }

    /// <summary>mInitRun.</summary>
    public bool InitRun { get; set; }

    /// <summary>mScript: the IScript running this code (its constructor sets it).</summary>
    public IScript? Script { get; set; }

    /// <summary>The destructor has run.</summary>
    public bool Deleted { get; private set; }

    public ScriptSymbol GetSymbol(int n) => Symbols[n];

    /// <summary>
    /// CWorld::GetCopyOfScript's CMissionScriptObjectCode::Clone, from the world's stored code (whose runtime values are
    /// CDataType::Create of the stored ones). The symbol table keeps the first symbol of each name; each event function
    /// finds its event names by symbol position in the copy. Pool nodes are taken in retail's order: each function's
    /// parameters, then the function's own node in the copy's list.
    /// </summary>
    public static ScriptCode Clone(ScriptObjectCode stored, SPtrSetPool pool, Action<string>? log = null)
    {
        var copy = new ScriptCode(stored, (int[])stored.EventPcs.Clone());

        // CSymbolTable::Clone: every symbol cloned; a name already present is not added (the clone is lost).
        foreach (Symbol s in stored.Symbols)
        {
            ScriptData? value = s.Value is null ? null : ScriptData.Create(s.Value);
            var symbol = new ScriptSymbol(s.Name, value, s.Type);
            if (copy.Find(symbol.Name) is null)
            {
                symbol.Index = copy.NextSymbolIndex;
                copy.Symbols.Add(symbol);
                copy.NextSymbolIndex++;
            }
            else
            {
                log?.Invoke($"symbol '{symbol}' dropped by CSymbolTable::Clone (duplicate name)");
            }
        }

        copy.EventFunctions = new SPtrSet<ScriptEventFunction>(pool);
        foreach (EventFunction f in stored.EventFunctions)
        {
            ScriptEventFunction function = CloneFunction(stored, f, copy, pool, log);
            copy.EventFunctions.Append(function);
        }

        copy.Unknown60 = stored.Unknown60;
        copy.Unknown5c = stored.Unknown5c;
        return copy;
    }

    /// <summary>
    /// CEventFunction::Clone of the world's function. The world's function kept (from its file constructor) the value of
    /// each string symbol it names; the clone finds that symbol's position n in the world's table and takes the copy's
    /// symbol n, if it holds an equal string.
    /// </summary>
    private static ScriptEventFunction CloneFunction(ScriptObjectCode stored, EventFunction f, ScriptCode copy,
        SPtrSetPool pool, Action<string>? log)
    {
        var function = new ScriptEventFunction(f.EntryPc, copy, pool);
        foreach (int index in f.ParameterSymbols)
        {
            ScriptValue? value = stored.Symbols[index].Value
                ?? throw new InvalidDataException($"event function at {f.EntryPc}: symbol {index} has no value (retail faults)");
            if (value.Type != DataType.String)
            {
                log?.Invoke("FATAL ERROR: Event Function was expecting a string");
                continue;
            }

            // The world's table holds one value object per symbol, so the first symbol holding it is symbol `index`.
            if (index >= copy.Symbols.Count)
            {
                throw new InvalidDataException($"event function at {f.EntryPc}: symbol {index} past the copy's table");
            }

            ScriptData? mine = copy.Symbols[index].Value;
            if (mine is not null && mine.Type == DataType.String && ScriptData.SameString(mine.GetString(), value.String))
            {
                function.AddParam(mine);
            }
            else
            {
                log?.Invoke("FATAL ERROR: Data type wrong type in clone for event function");
            }
        }

        return function;
    }

    /// <summary>CSymbolTable::Find: the first of the symbols added so far with that name (strcmp).</summary>
    public ScriptSymbol? Find(byte[] name)
    {
        for (int i = 0; i < NextSymbolIndex; i++)
        {
            if (ScriptData.SameString(Symbols[i].Name, name))
            {
                return Symbols[i];
            }
        }

        return null;
    }

    /// <summary>
    /// ~CMissionScriptObjectCode: the event functions are deleted (their readers in the named events go null), then the
    /// symbol table and its values.
    /// </summary>
    public void Delete()
    {
        if (Deleted)
        {
            return;
        }

        Deleted = true;
        if (EventFunctions is not null)
        {
            for (ScriptEventFunction? f = EventFunctions.First(); f is not null; f = EventFunctions.Next())
            {
                f.Delete();
            }

            EventFunctions.RemoveAll();
        }

        foreach (ScriptSymbol s in Symbols)
        {
            s.Value?.Delete();
            s.Value = null;
        }

        Unknown5c = 0;
    }
}
