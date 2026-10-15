// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Source-first catalog tests for <c>UBattleEngineDataManager</c>'s ordered
/// configuration set and the released lookup law already carried by
/// <see cref="RetailBattleEngineConfigurations"/>.
/// </summary>
public sealed class RetailBattleEngineConfigurationCatalogTests
{
    [Fact]
    public void Catalog_PreservesMaterializedRecordOrderAndCount()
    {
        RetailBattleEngineConfigurationRecord first = Configuration("Standard", 1000.0f);
        RetailBattleEngineConfigurationRecord second = Configuration("Aquila Prototype", 150.0f);

        var catalog = new RetailBattleEngineConfigurationCatalog([first, second]);

        Assert.Equal(2, catalog.Count);
        Assert.Equal([first, second], catalog.ToArray());
        Assert.Same(first, catalog[0]);
        Assert.Same(second, catalog[1]);
    }

    [Fact]
    public void GetConfiguration_ByIndexMatchesTheSourceDataManagerBoundary()
    {
        RetailBattleEngineConfigurationRecord first = Configuration("Standard", 1000.0f);
        RetailBattleEngineConfigurationRecord second = Configuration("Aquila Prototype", 150.0f);
        var catalog = new RetailBattleEngineConfigurationCatalog([first, second]);

        Assert.Same(first, catalog.GetConfiguration(-1));
        Assert.Same(first, catalog.GetConfiguration(0));
        Assert.Same(second, catalog.GetConfiguration(1));
        Assert.Null(catalog.GetConfiguration(2));
        Assert.Null(catalog.GetConfiguration(int.MaxValue));
    }

    [Fact]
    public void GetConfiguration_ByNameReusesCStringFirstMatchAndFallback()
    {
        RetailBattleEngineConfigurationRecord fallback = Configuration("Standard", 1000.0f);
        RetailBattleEngineConfigurationRecord firstMatch =
            Configuration("Aquila Prototype\0ignored", 150.0f);
        RetailBattleEngineConfigurationRecord duplicate =
            Configuration("Aquila Prototype", 151.0f);
        var catalog = new RetailBattleEngineConfigurationCatalog(
            [fallback, firstMatch, duplicate]);

        Assert.Same(firstMatch, catalog.GetConfiguration("Aquila Prototype"));
        Assert.Same(firstMatch, catalog.GetConfiguration("Aquila Prototype\0other"));
        Assert.Same(fallback, catalog.GetConfiguration("aquila prototype"));
        Assert.Same(fallback, catalog.GetConfiguration("missing"));
    }

    [Fact]
    public void EmptyCatalog_HasStableExplicitLookupBoundaries()
    {
        var catalog = new RetailBattleEngineConfigurationCatalog([]);

        Assert.Empty(catalog);
        Assert.Null(catalog.GetConfiguration(-1));
        Assert.Null(catalog.GetConfiguration(0));
        Assert.Null(catalog.GetConfiguration("Standard"));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = catalog[0]);
    }

    [Fact]
    public void MaterializedInputs_AreSnapshottedWithoutNormalizingStoreBits()
    {
        int[] heat = [0, 1, 2, -1, int.MinValue, int.MaxValue];
        float[] values =
        [
            0.0f,
            -0.0f,
            float.PositiveInfinity,
            float.NegativeInfinity,
            BitConverter.UInt32BitsToSingle(0x7FC00001u),
            BitConverter.UInt32BitsToSingle(0x00000001u),
        ];
        var record = new RetailBattleEngineConfigurationRecord("Raw stores", heat, values);

        heat[1] = 99;
        values[1] = 99.0f;

        Assert.Equal([0, 1, 2, -1, int.MinValue, int.MaxValue], record.StoreHeat);
        Assert.Equal(
            [0x00000000u, 0x80000000u, 0x7F800000u, 0xFF800000u, 0x7FC00001u, 0x00000001u],
            record.StoreValue.Select(BitConverter.SingleToUInt32Bits));
    }

    [Fact]
    public void MalformedMaterializedRecords_AreRejectedAtTheCatalogBoundary()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RetailBattleEngineConfigurationCatalog(null!));
        Assert.Throws<ArgumentException>(
            () => new RetailBattleEngineConfigurationCatalog([null!]));

        Assert.Throws<ArgumentNullException>(
            () => new RetailBattleEngineConfigurationRecord(
                null!, new int[RetailWeaponStores.StoreCount], new float[RetailWeaponStores.StoreCount]));
        Assert.Throws<ArgumentException>(
            () => new RetailBattleEngineConfigurationRecord(
                "not a byte: \u0100",
                new int[RetailWeaponStores.StoreCount],
                new float[RetailWeaponStores.StoreCount]));
        Assert.Throws<ArgumentNullException>(
            () => new RetailBattleEngineConfigurationRecord(
                "Standard", null!, new float[RetailWeaponStores.StoreCount]));
        Assert.Throws<ArgumentNullException>(
            () => new RetailBattleEngineConfigurationRecord(
                "Standard", new int[RetailWeaponStores.StoreCount], null!));
        Assert.Throws<ArgumentException>(
            () => new RetailBattleEngineConfigurationRecord(
                "Standard", new int[RetailWeaponStores.StoreCount - 1], new float[RetailWeaponStores.StoreCount]));
        Assert.Throws<ArgumentException>(
            () => new RetailBattleEngineConfigurationRecord(
                "Standard", new int[RetailWeaponStores.StoreCount], new float[RetailWeaponStores.StoreCount + 1]));
    }

    [Fact]
    public void NameLookup_RejectsNullWithoutChangingRetailFaultControls()
    {
        var catalog = new RetailBattleEngineConfigurationCatalog([Configuration("Standard", 1000.0f)]);

        Assert.Throws<ArgumentNullException>(() => catalog.GetConfiguration((string)null!));
        Assert.Throws<InvalidOperationException>(
            () => RetailBattleEngineConfigurations.GetConfiguration(
                new string?[] { null }, 1, 0, new string?[] { "Standard" }));
    }

    [Fact]
    public void ResolveConfiguration_ConsumesWorldNamesThroughTheExistingRetailLaw()
    {
        RetailBattleEngineConfigurationRecord fallback = Configuration("Standard", 1000.0f);
        RetailBattleEngineConfigurationRecord aquila = Configuration("Aquila Prototype", 150.0f);
        var catalog = new RetailBattleEngineConfigurationCatalog([fallback, aquila]);
        string?[] worldNames = new string?[RetailBattleEngineConfigurations.MaxConfigurations];
        worldNames[0] = "Aquila Prototype";
        worldNames[1] = "Standard";

        Assert.Same(aquila, catalog.ResolveConfiguration(worldNames, 2, 0));
        Assert.Same(fallback, catalog.ResolveConfiguration(worldNames, 2, 1));
        Assert.Same(aquila, catalog.ResolveConfiguration(worldNames, 2, -1));
        Assert.Same(aquila, catalog.ResolveConfiguration(worldNames, 2, 99));

        worldNames[0] = "missing";
        Assert.Same(fallback, catalog.ResolveConfiguration(worldNames, 1, 0));
        Assert.Throws<ArgumentNullException>(
            () => catalog.ResolveConfiguration(null!, 0, 0));
    }

    [Fact]
    public void DeterministicSha256_PinsOrderNamesAndRawStoreBits()
    {
        RetailBattleEngineConfigurationRecord standard = new(
            "Standard",
            [0, 1, 2, -1, int.MinValue, int.MaxValue],
            [
                0.0f,
                -0.0f,
                1.5f,
                BitConverter.UInt32BitsToSingle(0x7FC00001u),
                float.PositiveInfinity,
                BitConverter.UInt32BitsToSingle(0x00000001u),
            ]);
        RetailBattleEngineConfigurationRecord aquila = new(
            "Aquila Prototype",
            [0, 0, 1, 0, 1, 1],
            [2000.0f, 100.0f, 150.0f, 200.0f, 100.0f, 100.0f]);

        var firstReplay = new RetailBattleEngineConfigurationCatalog([standard, aquila]);
        var secondReplay = new RetailBattleEngineConfigurationCatalog(
            [
                new RetailBattleEngineConfigurationRecord(
                    standard.ConfigurationName, standard.StoreHeat, standard.StoreValue),
                new RetailBattleEngineConfigurationRecord(
                    aquila.ConfigurationName, aquila.StoreHeat, aquila.StoreValue),
            ]);
        var reversed = new RetailBattleEngineConfigurationCatalog([aquila, standard]);
        var changedStore = new RetailBattleEngineConfigurationCatalog(
            [standard, Configuration("Aquila Prototype", 151.0f)]);

        Assert.Equal(
            "4161281f8b45f79bd5777d9b55b6750095dcd1b0162c02ddbfa0f19a4d568088",
            firstReplay.DeterministicSha256);
        Assert.Equal(firstReplay.DeterministicSha256, secondReplay.DeterministicSha256);
        Assert.NotEqual(firstReplay.DeterministicSha256, reversed.DeterministicSha256);
        Assert.NotEqual(firstReplay.DeterministicSha256, changedStore.DeterministicSha256);
    }

    private static RetailBattleEngineConfigurationRecord Configuration(
        string name,
        float firstStoreValue)
    {
        int[] storeHeat = new int[RetailWeaponStores.StoreCount];
        float[] storeValue = Enumerable.Repeat(1000.0f, RetailWeaponStores.StoreCount).ToArray();
        storeValue[0] = firstStoreValue;
        return new RetailBattleEngineConfigurationRecord(name, storeHeat, storeValue);
    }
}
