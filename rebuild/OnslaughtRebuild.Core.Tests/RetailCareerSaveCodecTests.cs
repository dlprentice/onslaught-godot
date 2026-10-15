// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core.Tests;

public sealed class RetailCareerSaveCodecTests
{
    [Fact]
    public void Read_ConsumesTheReviewedGoldCareerFixture()
    {
        byte[] bytes = File.ReadAllBytes(FixturePath);

        RetailCareerSave career = RetailCareerSaveCodec.Read(bytes);

        Assert.Equal(10_004, career.ContainerLength);
        Assert.Equal(0x4BD1, career.VersionWord);
        Assert.Equal(1, career.CareerInProgress);
        Assert.Equal(43, career.CampaignNodes.Count);
        Assert.Equal(43, career.CompletedWorldCount);
        Assert.All(career.CampaignNodes, node => Assert.Equal((byte)'S', node.Grade));
        Assert.Equal(232, career.UnlockedGoodieCount);
        Assert.Equal(800, career.SuggestedWorldNumber);
    }

    [Fact]
    public void Read_RejectsAWrongVersionWord()
    {
        byte[] bytes = File.ReadAllBytes(FixturePath);
        bytes[0] = 0;
        bytes[1] = 0;

        RetailCareerSaveFormatException error = Assert.Throws<RetailCareerSaveFormatException>(
            () => RetailCareerSaveCodec.Read(bytes));

        Assert.Contains("0x4BD1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RejectsATruncatedContainer()
    {
        byte[] bytes = File.ReadAllBytes(FixturePath);

        RetailCareerSaveFormatException error = Assert.Throws<RetailCareerSaveFormatException>(
            () => RetailCareerSaveCodec.Read(bytes.AsSpan(0, bytes.Length - 1)));

        Assert.Contains("10,004", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RejectsMalformedCampaignStructure()
    {
        byte[] bytes = File.ReadAllBytes(FixturePath);
        bytes[0x16] = 0xE7;
        bytes[0x17] = 0x03;

        RetailCareerSaveFormatException error = Assert.Throws<RetailCareerSaveFormatException>(
            () => RetailCareerSaveCodec.Read(bytes));

        Assert.Contains("node 0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RejectsMalformedCampaignLinks()
    {
        byte[] bytes = File.ReadAllBytes(FixturePath);
        bytes[0x190A] = 42;

        RetailCareerSaveFormatException error = Assert.Throws<RetailCareerSaveFormatException>(
            () => RetailCareerSaveCodec.Read(bytes));

        Assert.Contains("link 0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RejectsMalformedNodeLinkIndices()
    {
        byte[] bytes = File.ReadAllBytes(FixturePath);
        bytes[0x0E] = 2;

        RetailCareerSaveFormatException error = Assert.Throws<RetailCareerSaveFormatException>(
            () => RetailCareerSaveCodec.Read(bytes));

        Assert.Contains("node 0 link indices", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RejectsUnknownCampaignLinkStates()
    {
        byte[] bytes = File.ReadAllBytes(FixturePath);
        bytes[0x1906] = 3;

        RetailCareerSaveFormatException error = Assert.Throws<RetailCareerSaveFormatException>(
            () => RetailCareerSaveCodec.Read(bytes));

        Assert.Contains("link state 3", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_PreservesTheExactContainerWithoutAliasingCallerBytes()
    {
        byte[] bytes = File.ReadAllBytes(FixturePath);
        byte[] expected = bytes.ToArray();

        RetailCareerSave career = RetailCareerSaveCodec.Read(bytes);
        bytes[0x249A] ^= 0xFF;

        Assert.Equal(expected, career.ContainerBytes.ToArray());
    }

    [Fact]
    public void Read_RepeatsTheSameProgressionSummaryDeterministically()
    {
        byte[] bytes = File.ReadAllBytes(FixturePath);

        RetailCareerSave first = RetailCareerSaveCodec.Read(bytes);
        RetailCareerSave second = RetailCareerSaveCodec.Read(bytes);

        Assert.Equal(first.ProgressionSummary, second.ProgressionSummary);
    }

    [Fact]
    public void Read_ExposesTheGoldCareersSelectableWorlds()
    {
        RetailCareerSave career = RetailCareerSaveCodec.Read(File.ReadAllBytes(FixturePath));

        Assert.Equal(43, career.SelectableWorldNumbers.Count);
        Assert.All(
            RetailWorldCatalog.Nodes,
            node => Assert.True(career.IsWorldSelectable(node.WorldNumber)));
        Assert.False(career.IsWorldSelectable(999));
    }

    [Fact]
    public void Read_DoesNotTreatACompleteBrokenParentAsSelectable()
    {
        byte[] bytes = File.ReadAllBytes(FixturePath);
        bytes[0x1906] = RetailCareerNodeLink.CompleteBroken;

        RetailCareerSave career = RetailCareerSaveCodec.Read(bytes);

        Assert.False(career.IsWorldSelectable(110));
    }

    [Fact]
    public void Codec_PublicSurfaceIsReadOnly()
    {
        string[] declaredMethods = typeof(RetailCareerSaveCodec)
            .GetMethods(System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal([nameof(RetailCareerSaveCodec.Read)], declaredMethods);
    }

    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "gold_career_save.bin");
}
