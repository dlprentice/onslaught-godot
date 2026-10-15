// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Collections;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace OnslaughtRebuild.Core;

/// <summary>
/// The source- and retail-bounded fields admitted for one already-materialized
/// <c>CBattleEngineData</c> record.
/// </summary>
/// <remarks>
/// <para>
/// Pinned source declares the six 32-bit <c>BOOL mStoreHeat</c> words, six
/// <c>float mStoreValue</c> capacities, and C-string configuration name at
/// <c>BattleEngineDataManager.h:33-40</c>; its memory-buffer loader reads the
/// store pairs at <c>BattleEngineDataManager.cpp:357-362</c>. The tracked
/// source-order data contract consumes all 1,514 shipped bytes as six version-12
/// records, while <see cref="RetailWeaponStores"/> independently carries the
/// six-slot count and the released configuration-capacity read at <c>+0x88</c>.
/// </para>
/// <para>
/// No other <c>CBattleEngineData</c> field is admitted here. In particular this
/// type does not claim a complete retail object layout, parse files, normalize
/// Win32 <c>BOOL</c> words, or canonicalize IEEE-754 values. The caller supplies
/// fields already materialized by an outer data owner and this immutable value
/// retains their raw words/bits.
/// </para>
/// </remarks>
public sealed class RetailBattleEngineConfigurationRecord
{
    private readonly ReadOnlyCollection<int> _storeHeat;
    private readonly ReadOnlyCollection<float> _storeValue;

    public RetailBattleEngineConfigurationRecord(
        string configurationName,
        IEnumerable<int> storeHeat,
        IEnumerable<float> storeValue)
    {
        ArgumentNullException.ThrowIfNull(configurationName);
        ArgumentNullException.ThrowIfNull(storeHeat);
        ArgumentNullException.ThrowIfNull(storeValue);
        if (configurationName.Any(character => character > byte.MaxValue))
        {
            throw new ArgumentException(
                "A retail char* name must contain only one-byte values.",
                nameof(configurationName));
        }

        int[] heat = storeHeat.ToArray();
        float[] values = storeValue.ToArray();
        if (heat.Length != RetailWeaponStores.StoreCount)
        {
            throw new ArgumentException(
                $"A configuration requires exactly {RetailWeaponStores.StoreCount} store-heat values.",
                nameof(storeHeat));
        }

        if (values.Length != RetailWeaponStores.StoreCount)
        {
            throw new ArgumentException(
                $"A configuration requires exactly {RetailWeaponStores.StoreCount} store values.",
                nameof(storeValue));
        }

        ConfigurationName = configurationName;
        _storeHeat = Array.AsReadOnly(heat);
        _storeValue = Array.AsReadOnly(values);
    }

    public string ConfigurationName { get; }

    public IReadOnlyList<int> StoreHeat => _storeHeat;

    public IReadOnlyList<float> StoreValue => _storeValue;
}

/// <summary>
/// Pure ordered state for already-materialized Battle Engine configuration
/// records. File parsing and filesystem ownership stay outside Core.
/// </summary>
/// <remarks>
/// <para>
/// <c>BattleEngineDataManager.h:243-324</c> owns the ordered set, count, and
/// index/name walks. <c>BattleEngineConfigurations.h:7-27</c> owns the fixed
/// 20-name world table. <see cref="ResolveConfiguration"/> delegates its clamp,
/// byte-exact C-string search, first-match behavior, miss fallback, and
/// null/fault ordering to <see cref="RetailBattleEngineConfigurations"/> rather
/// than introducing another lookup implementation.
/// </para>
/// <para>
/// The catalog accepts an empty set, rejects structurally malformed records,
/// and snapshots caller collections. It intentionally does not load retail
/// data, expose a mutable data-manager lifecycle, or claim the complete record
/// schema whose baseline-preserving parser/re-encoder remains open.
/// </para>
/// </remarks>
public sealed class RetailBattleEngineConfigurationCatalog :
    IReadOnlyList<RetailBattleEngineConfigurationRecord>
{
    private static readonly byte[] s_hashSchema = Encoding.ASCII.GetBytes(
        "BEA_CONFIG_CATALOG_V1\0");
    private readonly ReadOnlyCollection<RetailBattleEngineConfigurationRecord> _records;
    private readonly ReadOnlyCollection<string?> _configurationNames;

    public RetailBattleEngineConfigurationCatalog(
        IEnumerable<RetailBattleEngineConfigurationRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        RetailBattleEngineConfigurationRecord[] materialized = records.ToArray();
        if (materialized.Any(record => record is null))
        {
            throw new ArgumentException("A catalog cannot contain a null record.", nameof(records));
        }

        _records = Array.AsReadOnly(materialized);
        _configurationNames = Array.AsReadOnly(
            materialized.Select(record => (string?)record.ConfigurationName).ToArray());
        DeterministicSha256 = ComputeDeterministicSha256();
    }

    public int Count => _records.Count;

    public RetailBattleEngineConfigurationRecord this[int index] => _records[index];

    /// <summary>
    /// SHA-256 over a versioned, little-endian canonical projection of ordered
    /// names and every raw store word/float bit pattern. This is reconstruction
    /// state identity, not a retail hash format.
    /// </summary>
    public string DeterministicSha256 { get; }

    /// <summary>
    /// <c>UBattleEngineDataManager::GetConfiguration(int)</c>
    /// (<c>BattleEngineDataManager.h:272-287</c>): negative indexes become zero,
    /// while a positive index past the ordered set returns null.
    /// </summary>
    public RetailBattleEngineConfigurationRecord? GetConfiguration(int index)
    {
        if (index < 0)
        {
            index = 0;
        }

        return index < Count ? _records[index] : null;
    }

    /// <summary>
    /// Resolves a materialized name through the existing released lookup law:
    /// first byte-exact C-string match in order, then record zero on a miss.
    /// </summary>
    public RetailBattleEngineConfigurationRecord? GetConfiguration(string configurationName)
    {
        ArgumentNullException.ThrowIfNull(configurationName);
        return ResolveConfiguration([configurationName], configurationCount: 1, configurationId: 0);
    }

    /// <summary>
    /// Resolves one id from an already-materialized world configuration-name
    /// table through <see cref="RetailBattleEngineConfigurations.GetConfiguration"/>.
    /// The catalog adds no second clamp, search, C-string, or fallback law.
    /// </summary>
    public RetailBattleEngineConfigurationRecord? ResolveConfiguration(
        IReadOnlyList<string?> configurationNames,
        int configurationCount,
        int configurationId)
    {
        int? index = RetailBattleEngineConfigurations.GetConfiguration(
            configurationNames,
            configurationCount,
            configurationId,
            _configurationNames);

        return index is int resolved ? _records[resolved] : null;
    }

    public IEnumerator<RetailBattleEngineConfigurationRecord> GetEnumerator() =>
        _records.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private string ComputeDeterministicSha256()
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(s_hashSchema);
        AppendInt32(hash, Count);

        foreach (RetailBattleEngineConfigurationRecord record in _records)
        {
            AppendInt32(hash, record.ConfigurationName.Length);
            byte[] name = record.ConfigurationName.Select(character => (byte)character).ToArray();
            hash.AppendData(name);

            for (int store = 0; store < RetailWeaponStores.StoreCount; store++)
            {
                AppendInt32(hash, record.StoreHeat[store]);
                AppendUInt32(hash, BitConverter.SingleToUInt32Bits(record.StoreValue[store]));
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendUInt32(IncrementalHash hash, uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }
}
