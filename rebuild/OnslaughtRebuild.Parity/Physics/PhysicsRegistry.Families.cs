// SPDX-License-Identifier: GPL-3.0-or-later
// The physics script's other families (weapons, weapon modes, rounds, spawners, explosions, components): until their
// values are ported, each statement registers its definition's name so positions and name lookups match retail's.
// Adapted from bea-decomp src/WorldPhysicsManager.h (UPhysicsManager::Add* and the Get*Index lookups). Not in Stuart's
// release.

namespace OnslaughtRebuild.Parity.Physics;

public sealed partial class PhysicsRegistry
{
    public List<NamedDefinition> Weapons { get; } = [];
    public List<NamedDefinition> WeaponModes { get; } = [];
    public List<NamedDefinition> Rounds { get; } = [];
    public List<NamedDefinition> SpawnerDefinitions { get; } = [];
    public List<NamedDefinition> Explosions { get; } = [];

    /// <summary>Values of these families not applied yet (their ports are pending).</summary>
    public int PendingValues { get; private set; }

    partial void ExecuteOther(PhysicsStatement s)
    {
        switch (s.Type)
        {
            case PhysicsStatementType.Weapon: Weapons.Add(new NamedDefinition(s.Name)); break;
            case PhysicsStatementType.WeaponMode: WeaponModes.Add(new NamedDefinition(s.Name)); break;
            case PhysicsStatementType.Round: Rounds.Add(new NamedDefinition(s.Name)); break;
            case PhysicsStatementType.Spawner: SpawnerDefinitions.Add(new NamedDefinition(s.Name)); break;
            case PhysicsStatementType.Explosion: Explosions.Add(new NamedDefinition(s.Name)); break;
            case PhysicsStatementType.Component: Components.Add(new UnitData(s.Name)); break;
        }

        PendingValues += s.Values.Count;
    }

    private partial int ExplosionIndex(byte[] name) => IndexOf(Explosions, name, e => e.Name);

    private partial int WeaponIndex(byte[] name) => IndexOf(Weapons, name, w => w.Name);

    private partial int SpawnerIndex(byte[] name) => IndexOf(SpawnerDefinitions, name, s => s.Name);
}
