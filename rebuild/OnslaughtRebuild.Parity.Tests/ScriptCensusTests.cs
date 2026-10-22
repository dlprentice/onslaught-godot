// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.World;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class ScriptCensusTests(ITestOutputHelper output)
{
    [GameDataFact]
    public void Level100NativeCensus()
    {
        (WorldFile b, WorldFile w, _, _) = WorldFileTests.Level100Worlds();
        foreach ((string title, WorldFile f) in new[] { ("base", b), ("world", w) })
        {
            foreach ((byte[] name, ScriptObjectCode code) in f.Scripts)
            {
                var calls = code.Instructions.Where(i => i.Opcode == Opcode.Call)
                    .Select(i => $"{i.Attribute & 0xff}/{(i.Attribute >> 8) & 0xff}/{(i.Attribute >> 16) != 0}");
                var ops = code.Instructions.Select(i => i.Opcode).Distinct().OrderBy(o => o);
                var duplicates = code.Symbols.GroupBy(y => y.NameText).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                output.WriteLine($"dups\t{Encoding.ASCII.GetString(name)}\t{string.Join(",", duplicates)}");
                string? dump = Environment.GetEnvironmentVariable("SCRIPT_DUMP_DIR");
                if (dump is not null)
                {
                    var sb = new StringBuilder();
                    for (int s = 0; s < code.Symbols.Count; s++)
                    {
                        Symbol y = code.Symbols[s];
                        sb.AppendLine($"sym {s}\t{y.NameText}\t{y.Value}\ttype {y.Type} index {y.Index} flag {y.Flag}");
                    }

                    sb.AppendLine($"events {string.Join(",", code.EventPcs)}; next {code.NextSymbolIndex}");
                    foreach (EventFunction e in code.EventFunctions)
                    {
                        sb.AppendLine($"fn {e.EntryPc} ({string.Join(",", e.ParameterSymbols.Select(i => code.Symbols[i].Value))})");
                    }

                    for (int pc = 0; pc < code.Instructions.Count; pc++)
                    {
                        Instruction i = code.Instructions[pc];
                        string note = i.Opcode switch
                        {
                            Opcode.Push or Opcode.Pop or Opcode.GetTop => $"{code.Symbols[i.Attribute].NameText}={code.Symbols[i.Attribute].Value}",
                            Opcode.Call => $"#{i.Attribute & 0xff} args {(i.Attribute >> 8) & 0xff} ret {i.Attribute >> 16}",
                            _ => "",
                        };
                        sb.AppendLine($"{pc,4} {i.Opcode} {i.Attribute} {note}");
                    }

                    File.WriteAllText(Path.Combine(dump, $"{title}-{Encoding.ASCII.GetString(name)}.txt"), sb.ToString());
                }

                output.WriteLine($"{title}\t{Encoding.ASCII.GetString(name)}\t{code.Instructions.Count}\t5c={code.Unknown5c} 60={code.Unknown60}\tpcs={string.Join(",", code.EventPcs)}\tfns={code.EventFunctions.Count}\tops={string.Join(",", ops)}\tcalls={string.Join(" ", calls)}");
            }
        }
    }
}
