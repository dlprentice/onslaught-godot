// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>UBattleEngineConfigurations::GetConfiguration</c> — the released
/// chassis-configuration lookup, with the two fallbacks that keep it from
/// returning nothing.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/BattleEngineConfigurations.cpp:80-93</c> over
/// <c>BattleEngineDataManager.h:272-298</c>, with the array bound at
/// <c>BattleEngineConfigurations.h:7</c>. Retail identity: <c>0x0040F2F0</c> in
/// the pristine <c>74154bfa…</c> image, file offset = VA - 0x400000. Both
/// statics are in <c>.data</c>: <c>sConfigurationName</c> at <c>0x00660200</c>
/// and <c>sConfigurations</c> at <c>0x00660250</c>.
/// </para>
/// <para>
/// <b><c>kMaxConfigurations</c> is measured, not taken from the header.</b>
/// <c>UBattleEngineConfigurations::ShutDown</c> (<c>0x0040F140</c>) walks
/// <c>esi</c> from <c>0x00660200</c> to <c>0x00660250</c> in steps of four
/// (<c>cmp esi, 0x660250 / jl</c> at <c>0x0040F16A</c>), which is
/// <c>0x50 / 4 = 20</c> pointers — and the counter it clears first sits at
/// exactly the address the loop stops at, so the two are adjacent and the bound
/// is not a coincidence. That agrees with the header's <c>20</c>.
/// </para>
/// <para>
/// <b>Source and retail agree on the clamp, the search and both fallbacks.</b>
/// <c>0x0040F2F4-0x0040F300</c> is <c>test eax, eax / jl</c> then
/// <c>cmp eax, [0x00660250] / jl</c> with a shared <c>xor eax, eax</c> —
/// <c>(id &lt; 0) || (id &gt;= sConfigurations)</c> becomes <c>0</c>, exactly
/// <c>BattleEngineConfigurations.cpp:84-85</c>. The search is a linear walk of
/// the data set with an inline <c>strcmp</c>, and the miss path falls into a
/// second walk with the index counter zeroed, which is
/// <c>UBattleEngineDataManager::GetConfiguration(0)</c> constant-folded:
/// <c>if (inIndex == 0) return data</c> on the first element, so the fallback is
/// "the first configuration loaded", and <c>NULL</c> only when the set is empty.
/// </para>
/// <para>
/// <b>The name comparison is byte-exact, case-sensitive and unsigned.</b>
/// <c>0x0040F339-0x0040F360</c> is MSVC's inline <c>strcmp</c> intrinsic — two
/// bytes per iteration, <c>cmp</c> on <c>al</c> then
/// <c>sbb ecx, ecx / sbb ecx, -1</c> to turn the borrow into <c>±1</c>. The
/// borrow flag makes the <i>ordering</i> unsigned, so a byte of <c>0x80</c> or
/// above sorts high; only equality is consumed here, so what matters is that
/// there is no case folding and no length limit, and that comparison stops at
/// the first NUL in <b>either</b> string. That last point is why
/// <see cref="CStringEquals"/> truncates: a C string cannot carry an embedded
/// NUL, and two names that differ only past one compare equal.
/// </para>
/// <para>
/// <b>A missing name is a crash, and the ordering of the two fallbacks decides
/// whether it happens.</b> <c>Initialise</c> fills
/// <c>sConfigurationName</c> with null pointers and only <c>Load</c> fills them
/// in, so an id clamped to <c>0</c> against a <c>sConfigurations</c> of zero
/// hands the search a null name. Retail then reaches
/// <c>cmp al, byte ptr [esi]</c> at <c>0x0040F33D</c> with <c>esi</c> zero and
/// faults — but <b>only if the data set is non-empty</b>, because the emptiness
/// test at <c>0x0040F32D</c> comes first and jumps straight to the fallback
/// without touching the name. So "no configuration names loaded" is survivable
/// when no configuration data is loaded either, and fatal when data was loaded
/// without names. <see cref="GetConfiguration"/> throws on that path rather than
/// inventing a return value, the same way
/// <see cref="RetailWeaponCycle.ChangeWeapon"/> does.
/// </para>
/// <para>
/// <b>The clamp is not a bounds check.</b> It compares against
/// <c>sConfigurations</c> — the number of names <c>Load</c> read from the file —
/// and not against <c>kMaxConfigurations</c>. <c>Load</c> itself writes
/// <c>sConfigurationName[n]</c> for <c>n &lt; sConfigurations</c> with no
/// ceiling (<c>BattleEngineConfigurations.cpp:36-51</c>), so a data file
/// claiming more than twenty configurations overruns the array before
/// <c>GetConfiguration</c> is ever called. That is a <c>Load</c> problem, noted
/// here because a reader of the clamp may assume it is the guard.
/// </para>
/// <para>
/// <b>There is exactly one caller.</b> A scan of every <c>E8</c>-relative call
/// in the image finds one site targeting <c>0x0040F2F0</c>, so the id this
/// receives is not a broad surface. Which caller that is, and where its id comes
/// from, is not established here.
/// </para>
/// <para>
/// <b>Not established here.</b> <c>sData</c> is a
/// <c>CSPtrSet&lt;CBattleEngineData&gt;</c> and its walk is the pointer plumbing
/// this lane does not model; it is flattened below to an ordered list of names,
/// which is what <c>First()</c>/<c>Next()</c> reduce to for a set whose nodes all
/// carry payloads. <c>mConfigurationName</c> lives at
/// <c>data + 0xA8</c> (<c>0x0040F333</c>); nothing here claims the rest of
/// <c>CBattleEngineData</c>.
/// </para>
/// </remarks>
public static class RetailBattleEngineConfigurations
{
    /// <summary>
    /// <c>kMaxConfigurations</c> — <c>(0x00660250 - 0x00660200) / 4</c>, the
    /// <c>ShutDown</c> loop bound.
    /// </summary>
    public const int MaxConfigurations = 20;

    /// <summary><c>sConfigurationName</c> — <c>0x00660200</c>.</summary>
    public const uint ConfigurationNameArrayAddress = 0x00660200u;

    /// <summary><c>sConfigurations</c> — <c>0x00660250</c>.</summary>
    public const uint ConfigurationCountAddress = 0x00660250u;

    /// <summary>
    /// <c>BattleEngineConfigurations.cpp:84-85</c> —
    /// <c>0x0040F2F4-0x0040F300</c>. Anything outside
    /// <c>[0, configurationCount)</c> becomes <c>0</c>, including a
    /// <c>configurationCount</c> of zero.
    /// </summary>
    public static int ClampConfigurationId(int configurationId, int configurationCount) =>
        configurationId < 0 || configurationId >= configurationCount ? 0 : configurationId;

    /// <summary>
    /// <c>strcmp(a, b) == 0</c> as the inline intrinsic at
    /// <c>0x0040F339</c> computes it: byte-exact, case-sensitive, and stopping at
    /// the first NUL in either operand.
    /// </summary>
    public static bool CStringEquals(string left, string right)
    {
        if (left is null)
        {
            throw new ArgumentNullException(nameof(left));
        }

        if (right is null)
        {
            throw new ArgumentNullException(nameof(right));
        }

        return string.Equals(CStringPrefix(left), CStringPrefix(right), StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>UBattleEngineConfigurations::GetConfiguration</c> —
    /// <c>BattleEngineConfigurations.cpp:80-93</c>, <c>0x0040F2F0</c>.
    /// </summary>
    /// <param name="configurationNames">
    /// <c>sConfigurationName</c>. <c>null</c> entries are the pointers
    /// <c>Initialise</c> leaves behind.
    /// </param>
    /// <param name="configurationCount">
    /// <c>sConfigurations</c>. Deliberately separate from
    /// <paramref name="configurationNames"/>'s length, because retail's clamp
    /// uses this and not the array bound.
    /// </param>
    /// <param name="configurationId">The requested id, before the clamp.</param>
    /// <param name="dataNames">
    /// <c>data-&gt;mConfigurationName</c> for each element of <c>sData</c>, in
    /// walk order.
    /// </param>
    /// <returns>
    /// The index into <paramref name="dataNames"/> retail returns, or
    /// <c>null</c> for its <c>0L</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// When the clamped name is <c>null</c> and the data set is non-empty:
    /// retail dereferences it at <c>0x0040F33D</c>.
    /// </exception>
    public static int? GetConfiguration(
        IReadOnlyList<string?> configurationNames,
        int configurationCount,
        int configurationId,
        IReadOnlyList<string?> dataNames)
    {
        if (configurationNames is null)
        {
            throw new ArgumentNullException(nameof(configurationNames));
        }

        if (dataNames is null)
        {
            throw new ArgumentNullException(nameof(dataNames));
        }

        string? name = configurationNames[ClampConfigurationId(configurationId, configurationCount)];

        // 0x0040F32D: the emptiness test comes before the name is ever read.
        if (dataNames.Count == 0)
        {
            return null;
        }

        if (name is null)
        {
            throw new InvalidOperationException(
                "sConfigurationName[id] is null and sData is not empty; retail reads " +
                "byte ptr [esi] at 0x0040F33D with esi zero and faults.");
        }

        for (int index = 0; index < dataNames.Count; index++)
        {
            string? dataName = dataNames[index];
            if (dataName is null)
            {
                throw new InvalidOperationException(
                    "data->mConfigurationName is null; retail reads byte ptr [ecx] " +
                    "at 0x0040F339 and faults.");
            }

            if (CStringEquals(dataName, name))
            {
                return index;
            }
        }

        // 0x0040F380: GetConfiguration(0) folded to "the first element".
        return 0;
    }

    /// <summary>The bytes a C string actually occupies — everything before the first NUL.</summary>
    private static string CStringPrefix(string value)
    {
        int terminator = value.IndexOf('\0');
        return terminator < 0 ? value : value.Substring(0, terminator);
    }
}
