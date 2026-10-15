// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins <c>CMenuItem__Render</c> <c>0x004A33FC</c> <c>and esi, ebp</c>,
/// recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para>Selected is <c>0xFFFFCC00</c>. Disabled is <c>0x50505050</c> and
/// overrides selected. Idle is <c>[this+0x14] = 0xFFD6D6D6</c>. Incoming
/// <c>-1</c> from <c>RenderCentered</c> leaves the base. Apply's cosine
/// is the non-identity incoming. Dropdown does not use this AND.</para>
/// </summary>
public sealed class RetailOptionsMenuItemColorTests
{
    [Fact]
    public void SpecimenSitesAreRenderAndAndEsiEbp()
    {
        Assert.Equal(0x004A32C0u, RetailOptionsMenuItemColor.RenderSite);
        Assert.Equal(0x004A33FCu, RetailOptionsMenuItemColor.AndSite);
        Assert.Equal(0x14u, RetailOptionsMenuItemColor.IdleColorOffset);
        Assert.Equal(0x10u, RetailOptionsMenuItemColor.EnabledOffset);
        Assert.Equal(0xFFD6D6D6u, RetailOptionsMenuItemColor.IdlePackedColor);
        Assert.Equal(0xFFFFCC00u, RetailOptionsMenuItemColor.SelectedPackedColor);
        Assert.Equal(0x50505050u, RetailOptionsMenuItemColor.DisabledPackedColor);
        Assert.Equal(0xFFFFFFFFu, RetailOptionsMenuItemColor.IncomingIdentity);
    }

    [Fact]
    public void IdentityIncomingLeavesSelectedIdleAndDisabled()
    {
        Assert.Equal(
            0xFFD6D6D6u,
            RetailOptionsMenuItemColor.PackedColor(selected: false, enabled: true, 0xFFFFFFFFu));
        Assert.Equal(
            0xFFFFCC00u,
            RetailOptionsMenuItemColor.PackedColor(selected: true, enabled: true, 0xFFFFFFFFu));
        Assert.Equal(
            0x50505050u,
            RetailOptionsMenuItemColor.PackedColor(selected: true, enabled: false, 0xFFFFFFFFu));
        Assert.Equal(
            0x50505050u,
            RetailOptionsMenuItemColor.PackedColor(selected: false, enabled: false, 0xFFFFFFFFu));
    }

    [Fact]
    public void ApplyPulseIncomingIsAndedWithTheRowBase()
    {
        // 0xFFFFCC00 & 0xFFcccccc = 0xFFcccc00; 0xFFD6D6D6 & 0xFF000000 = 0xFF000000.
        uint whitePulse = RetailOptionsApplyPulse.PackedColor(pending: true, 0.5f);
        uint blackPulse = RetailOptionsApplyPulse.PackedColor(pending: true, 0f);
        Assert.Equal(0xFFFFFFFFu, whitePulse);
        Assert.Equal(0xFF000000u, blackPulse);
        Assert.Equal(
            0xFFFFCC00u,
            RetailOptionsMenuItemColor.PackedColor(selected: true, enabled: true, whitePulse));
        Assert.Equal(
            0xFFCCCC00u,
            RetailOptionsMenuItemColor.PackedColor(selected: true, enabled: true, 0xFFCCCCCCu));
        Assert.Equal(
            0xFF000000u,
            RetailOptionsMenuItemColor.PackedColor(selected: true, enabled: true, blackPulse));
        Assert.Equal(
            0xFFD6D6D6u,
            RetailOptionsMenuItemColor.PackedColor(selected: false, enabled: true, whitePulse));
        Assert.Equal(
            0xFF000000u,
            RetailOptionsMenuItemColor.PackedColor(selected: false, enabled: true, blackPulse));
    }

    [Fact]
    public void DrawOptionRowAndsApplyAndDoesNotAndDropdown()
    {
        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        Assert.Contains("RetailOptionsMenuItemColor.PackedColor", draw, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsAction.Apply", draw, StringComparison.Ordinal);
        Assert.Contains("DropdownRowIsPending", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
    }

    private static string Slice(string source, string header)
    {
        int start = source.IndexOf(header, StringComparison.Ordinal);
        Assert.True(start >= 0, header);
        int next = source.IndexOf("\n    private ", start + header.Length, StringComparison.Ordinal);
        return next < 0 ? source[start..] : source[start..next];
    }
}
