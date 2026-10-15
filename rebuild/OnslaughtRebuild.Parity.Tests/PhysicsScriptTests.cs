// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Physics;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class PhysicsScriptTests
{
    internal static PhysicsScript Load()
    {
        (string name, byte[] bytes) = GameData.Read(@"data\default physics.dat");
        return PhysicsScript.Read(MemBuffer.FromFile(name, bytes));
    }

    [GameDataFact]
    public void TheDefaultPhysicsScriptReadsToItsEnd()
    {
        (string name, byte[] bytes) = GameData.Read(@"data\default physics.dat");
        MemBuffer file = MemBuffer.FromFile(name, bytes);
        PhysicsScript script = PhysicsScript.Read(file);

        Assert.Equal((uint)bytes.Length, file.Position);
        Assert.Equal(777, script.Statements.Count);
        Assert.Equal(160, script.Statements.Count(s => s.Type == PhysicsStatementType.Unit));
        Assert.Contains(script.Statements, s => s.Type == PhysicsStatementType.Unit && s.NameText == "Target Tank");
        Assert.Contains(script.Statements, s => s.Type == PhysicsStatementType.Unit && s.NameText == "Control Tower");
    }
}
