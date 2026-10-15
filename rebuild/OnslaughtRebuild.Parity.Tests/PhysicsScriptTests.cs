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

public sealed class PhysicsRegistryTests
{
    [GameDataFact]
    public void TheLevel100UnitsHaveTheirBehaviours()
    {
        PhysicsRegistry r = PhysicsRegistry.Build(PhysicsScriptTests.Load());

        Assert.Equal(0, r.SkippedValues);
        Assert.Equal(160, r.Units.Count);
        UnitBehaviour Of(string name) => r.GetUnitData(System.Text.Encoding.ASCII.GetBytes(name))!.Behaviour;
        Assert.Equal(UnitBehaviour.Building, Of("Control Tower"));
        Assert.Equal(UnitBehaviour.Turret, Of("SAT Turret"));
        Assert.Equal(UnitBehaviour.GroundUnit, Of("Target Tank"));
        Assert.Equal(UnitBehaviour.Dropship, Of("U-17 Highside Transporter"));
        Assert.Equal(UnitBehaviour.Fighter, Of("Air Trainer"));
        Assert.Equal(UnitBehaviour.SimpleBuilding, Of("Forseti City Building 1"));
    }

    [GameDataFact]
    public void UnitNamesAreUniqueSoEachStatementAppliesToItsOwnDefinition()
    {
        PhysicsScript script = PhysicsScriptTests.Load();
        var names = script.Statements.Where(s => s.Type == PhysicsStatementType.Unit).Select(s => s.NameText).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }
}
