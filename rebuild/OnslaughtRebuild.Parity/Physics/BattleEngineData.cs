// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/BattleEngineDataManager.h and src/BattleEngineDataManager.cpp (CBattleEngineData: the
// constructor 0040f520, Initialise 0040f590, Shutdown 0040f890, Load 0040f980; UBattleEngineDataManager: Initialise,
// Load, GetConfiguration) and src/BattleEngineConfigurations.cpp (UBattleEngineConfigurations::GetConfiguration
// 0040f2f0), in Stuart's release.

using System.Text;
using OnslaughtRebuild.Parity.Data;

namespace OnslaughtRebuild.Parity.Physics;

/// <summary>CBattleEngineData: one battle engine configuration (data\battle engine configurations.dat).</summary>
public sealed class BattleEngineData
{
    /// <summary>kBattleEngineStores.</summary>
    public const int Stores = 6;

    public float MaxAirVelocity;
    public float MinAirVelocity;
    public float GroundVelocity;
    public float MaxAirEnergyCost;
    public float MinAirEnergyCost;
    public float AirTurnRate;
    public float GroundTurnRate;
    public float Life;
    public float Energy;
    public float ShieldEfficiency;
    public float GroundEnergyIncrease;
    public float MinTransformEnergy;
    public float WalkFriction;
    public float MaxWalkVelocity;
    public float RollEnergyCost;
    public float LoopEnergyCost;

    /// <summary>mWalkerWeapons / mJetWeapons in list order (SPtrSet: Add prepends, Append appends).</summary>
    public List<byte[]> WalkerWeapons { get; } = [];

    public List<byte[]> JetWeapons { get; } = [];

    public byte[]? PrimaryWeapon;
    public byte[]? AugWeapon;
    public byte[]? Explosion;
    public byte[]? Cockpit;
    public readonly int[] StoreHeat = new int[Stores];
    public readonly float[] StoreValue = new float[Stores];
    public float Stealth;
    public int LanguageName;
    public byte[]? ConfigurationName;

    public string? CockpitText => Cockpit is null ? null : Encoding.ASCII.GetString(Cockpit);

    public string? NameText => ConfigurationName is null ? null : Encoding.ASCII.GetString(ConfigurationName);

    /// <summary>CBattleEngineData::Initialise: the "Standard" configuration's defaults.</summary>
    public void Initialise()
    {
        ConfigurationName = "Standard"u8.ToArray();
        WalkerWeapons.Insert(0, "Vulcan Cannon 1"u8.ToArray());
        WalkerWeapons.Insert(0, "Pulse Cannon Pod"u8.ToArray());
        JetWeapons.Insert(0, "Vulcan Cannon 1"u8.ToArray());
        JetWeapons.Insert(0, "Missile Pod"u8.ToArray());
        Array.Fill(StoreHeat, 0);
        Array.Fill(StoreValue, 1000);
        Explosion = "Animated Explosion Emitter 2"u8.ToArray();
        AugWeapon = [];
        PrimaryWeapon = [];
        Cockpit = "cockpit2.msh"u8.ToArray();
        Life = 20.0f;
        Energy = 2.5f;
        GroundEnergyIncrease = 0.01f;
        MinAirEnergyCost = 0.1f;
        MaxAirEnergyCost = 0.3f;
        MinTransformEnergy = 1.0f;
        MaxAirVelocity = 7.5f;
        MinAirVelocity = 5.0f;
        GroundVelocity = 4.0f;
        AirTurnRate = 2.0f;
        GroundTurnRate = 1.5f;
        ShieldEfficiency = 90.0f;
        WalkFriction = 0.9f;
        MaxWalkVelocity = 0.15f;
        RollEnergyCost = 1.0f;
        LoopEnergyCost = 1.0f;
        LanguageName = 1;
        Stealth = 0;
    }

    /// <summary>CBattleEngineData::Shutdown: the names and weapon lists go; the numbers stay.</summary>
    public void Shutdown()
    {
        ConfigurationName = null;
        JetWeapons.Clear();
        WalkerWeapons.Clear();
        Explosion = null;
        AugWeapon = null;
        PrimaryWeapon = null;
        Cockpit = null;
    }

    /// <summary>CBattleEngineData::Load (the CMEMBUFFER version).</summary>
    public void Load(MemBuffer f)
    {
        Shutdown();
        int version = f.ReadInt32();
        Life = f.ReadSingle();
        Energy = f.ReadSingle();
        GroundEnergyIncrease = f.ReadSingle();
        MaxAirEnergyCost = f.ReadSingle();
        MinTransformEnergy = f.ReadSingle();
        MaxAirVelocity = f.ReadSingle();
        GroundVelocity = f.ReadSingle();
        AirTurnRate = f.ReadSingle();
        GroundTurnRate = f.ReadSingle();
        if (version < 8)
        {
            f.ReadSingle();
            f.ReadSingle();
            f.ReadSingle();
        }

        if (version <= 4)
        {
            JetWeapons.Insert(0, f.ReadZeroTerminated());
            JetWeapons.Insert(0, f.ReadZeroTerminated());
            WalkerWeapons.Insert(0, f.ReadZeroTerminated());
            WalkerWeapons.Insert(0, f.ReadZeroTerminated());
            WalkerWeapons.Insert(0, f.ReadZeroTerminated());
        }

        ConfigurationName = f.ReadZeroTerminated();
        if (version <= 4)
        {
            for (int i = 0; i < 4; i++)
            {
                f.ReadSingle();
            }
        }

        if (version > 1)
        {
            ShieldEfficiency = f.ReadSingle();
        }

        if (version > 2)
        {
            Stealth = f.ReadSingle();
        }

        Explosion = version > 3 ? f.ReadZeroTerminated() : "Animated Explosion Emitter 2"u8.ToArray();
        if (version > 4)
        {
            for (int n = f.ReadInt32(); n != 0; n--)
            {
                WalkerWeapons.Add(f.ReadZeroTerminated());
            }

            for (int n = f.ReadInt32(); n != 0; n--)
            {
                JetWeapons.Add(f.ReadZeroTerminated());
            }

            for (int n = 0; n < Stores; n++)
            {
                StoreHeat[n] = f.ReadInt32();
                StoreValue[n] = f.ReadSingle();
            }
        }

        if (version > 5)
        {
            MinAirVelocity = f.ReadSingle();
        }

        if (version > 6)
        {
            MaxWalkVelocity = f.ReadSingle();
            WalkFriction = f.ReadSingle();
        }

        if (version > 7)
        {
            MinAirEnergyCost = f.ReadSingle();
            RollEnergyCost = f.ReadSingle();
            LoopEnergyCost = f.ReadSingle();
        }

        if (version < 8)
        {
            MinAirVelocity = 0.3f;
            MaxAirVelocity = 0.9f;
            MinAirEnergyCost = 0.005f;
            MaxAirEnergyCost = 0.015f;
        }

        if (version > 8)
        {
            AugWeapon = f.ReadZeroTerminated();
        }

        if (version > 9)
        {
            PrimaryWeapon = f.ReadZeroTerminated();
        }

        Cockpit = version > 10 ? f.ReadZeroTerminated() : "cockpit2.msh"u8.ToArray();
        LanguageName = version > 11 ? f.ReadInt32() : 1;
    }
}

/// <summary>
/// UBattleEngineDataManager: the configurations in their list order. Load replaces the default with the file's
/// configurations, each added at the head of the list (SPtrSet::Add), so index 0 is the file's last.
/// </summary>
public sealed class BattleEngineDataManager
{
    public List<BattleEngineData> Configurations { get; } = [];

    /// <summary>Initialise then Load: the manager CGame builds at start-up (WorldPhysicsManager.cpp).</summary>
    public static BattleEngineDataManager Load(MemBuffer file)
    {
        var m = new BattleEngineDataManager();
        int count = file.ReadInt32();
        for (int n = 0; n < count; n++)
        {
            var config = new BattleEngineData();
            config.Initialise();
            config.Load(file);
            m.Configurations.Insert(0, config);
        }

        return m;
    }

    /// <summary>GetConfiguration(int): the configuration at that position, a negative index meaning 0.</summary>
    public BattleEngineData? GetConfiguration(int index)
    {
        if (index < 0)
        {
            index = 0;
        }

        return index < Configurations.Count ? Configurations[index] : null;
    }

    /// <summary>GetConfiguration(char *): the first configuration with that name (exact).</summary>
    public BattleEngineData? GetConfiguration(string name) =>
        Configurations.FirstOrDefault(c => c.NameText == name);

    /// <summary>
    /// UBattleEngineConfigurations::GetConfiguration: the world header's configuration names by id (an id out of range
    /// means 0), looked up by name, else the first configuration.
    /// </summary>
    public BattleEngineData? ForWorld(IReadOnlyList<byte[]> worldConfigurations, int id)
    {
        if (id < 0 || id >= worldConfigurations.Count)
        {
            id = 0;
        }

        return (worldConfigurations.Count > 0 ? GetConfiguration(Encoding.ASCII.GetString(worldConfigurations[id])) : null)
            ?? GetConfiguration(0);
    }
}
