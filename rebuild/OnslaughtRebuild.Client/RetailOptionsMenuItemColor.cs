// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItem__Render</c> packed colour combine — recovered from the
/// pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this cycle
/// (2,506,752 bytes).
///
/// <para><b>Base.</b> <c>0x004A32CF</c> loads ESI from <c>[this+0x14]</c>.
/// <c>CMenuItem__Init</c> <c>0x004A354B</c> writes
/// <see cref="IdlePackedColor"/>. <c>0x004A335E</c> is
/// <c>test [selected]; je; mov esi, 0xFFFFCC00</c>. <c>0x004A336B</c> is
/// <c>test [this+0x10]; jne; mov esi, 0x50505050</c> and overrides
/// selected.</para>
///
/// <para><b>Combine.</b> <c>0x004A33FC</c> is <c>and esi, ebp</c>. EBP is
/// the incoming ARGB. <c>CMenuItem__RenderCentered</c> <c>0x004A3275</c>
/// pushes <c>-1</c>, so the AND is identity. Apply's pulse is the
/// non-identity incoming. Dropdown has its own Render and does not use
/// this AND. This is not the 0x00463E8D twin fade and not
/// <c>SetLanguage</c>.</para>
/// </summary>
public static class RetailOptionsMenuItemColor
{
    /// <summary><c>CMenuItem__Render</c> body at <c>0x004A32C0</c>.</summary>
    public const uint RenderSite = 0x004A32C0u;

    /// <summary><c>and esi, ebp</c> at <c>0x004A33FC</c>.</summary>
    public const uint AndSite = 0x004A33FCu;

    /// <summary><c>mov esi, [edi+0x14]</c> at <c>0x004A32CF</c>.</summary>
    public const uint IdleColorOffset = 0x14u;

    /// <summary><c>CMenuItem__Init</c> <c>mov [esi+0x14], 0xFFD6D6D6</c>.</summary>
    public const uint IdlePackedColor = 0xFFD6D6D6u;

    /// <summary><c>mov esi, 0xFFFFCC00</c> at <c>0x004A3366</c>.</summary>
    public const uint SelectedPackedColor = 0xFFFFCC00u;

    /// <summary><c>test [edi+0x10]</c> at <c>0x004A336B</c>.</summary>
    public const uint EnabledOffset = 0x10u;

    /// <summary><c>mov esi, 0x50505050</c> at <c>0x004A3372</c>.</summary>
    public const uint DisabledPackedColor = 0x50505050u;

    /// <summary>
    /// <c>CMenuItem__RenderCentered</c> <c>push -1</c> at <c>0x004A3275</c>.
    /// </summary>
    public const uint IncomingIdentity = 0xFFFFFFFFu;

    /// <summary>
    /// ESI after the selected / disabled writes, before the AND.
    /// Disabled overrides selected.
    /// </summary>
    public static uint BaseColor(bool selected, bool enabled)
    {
        if (!enabled)
        {
            return DisabledPackedColor;
        }

        return selected ? SelectedPackedColor : IdlePackedColor;
    }

    /// <summary>
    /// <c>and esi, ebp</c>. Incoming is the caller ARGB; Apply's pulse is
    /// the non-identity case.
    /// </summary>
    public static uint PackedColor(bool selected, bool enabled, uint incoming) =>
        BaseColor(selected, enabled) & incoming;
}
