// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;

namespace OnslaughtRebuild.Core;

/// <summary>
/// One campaign node projected from the released PC <c>CCareer</c> block.
/// Stuart's owner is <c>Career.h:76-100</c>; the retail record offsets and
/// 0x40-byte stride are measured in
/// <c>reverse-engineering/save-file/save-format.md</c>.
/// </summary>
public sealed record RetailCareerReadNode(
    int Index,
    int WorldNumber,
    int Complete,
    int LowerLink,
    int HigherLink,
    int NumAttempts,
    float Ranking,
    byte Grade);

/// <summary>The released progression fields needed by the current frontend slice.</summary>
public readonly record struct RetailCareerProgressionSummary(
    int CareerInProgress,
    int CompletedWorldCount,
    int UnlockedGoodieCount,
    int SuggestedWorldNumber);

/// <summary>A supplied byte span is not the supported released-PC career container.</summary>
public sealed class RetailCareerSaveFormatException : FormatException
{
    public RetailCareerSaveFormatException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Immutable read projection of one supported released-PC career container.
/// The save name and storage slot are frontend metadata and are deliberately
/// not inferred from these bytes.
/// </summary>
public sealed class RetailCareerSave
{
    private readonly byte[] _containerBytes;

    internal RetailCareerSave(
        byte[] containerBytes,
        ushort versionWord,
        int containerLength,
        int careerInProgress,
        IReadOnlyList<RetailCareerReadNode> campaignNodes,
        int unlockedGoodieCount,
        IReadOnlyList<int> selectableWorldNumbers,
        int suggestedWorldNumber)
    {
        _containerBytes = containerBytes;
        VersionWord = versionWord;
        ContainerLength = containerLength;
        CareerInProgress = careerInProgress;
        CampaignNodes = campaignNodes;
        UnlockedGoodieCount = unlockedGoodieCount;
        SelectableWorldNumbers = selectableWorldNumbers;
        SuggestedWorldNumber = suggestedWorldNumber;
    }

    public ushort VersionWord { get; }

    public int ContainerLength { get; }

    /// <summary>
    /// The exact supported container, including reserved and unknown bytes.
    /// The reader owns a private copy and exposes no writable memory.
    /// </summary>
    public ReadOnlySpan<byte> ContainerBytes => _containerBytes;

    public int CareerInProgress { get; }

    public IReadOnlyList<RetailCareerReadNode> CampaignNodes { get; }

    public int CompletedWorldCount => CampaignNodes.Count(node => node.Complete == 1);

    public int UnlockedGoodieCount { get; }

    public IReadOnlyList<int> SelectableWorldNumbers { get; }

    public bool IsWorldSelectable(int worldNumber) =>
        SelectableWorldNumbers.Contains(worldNumber);

    /// <summary>
    /// The last selectable campaign row. Retail's normal career load passes
    /// flag 1 and asks the frontend to select the latest unlocked world
    /// (<c>FEPLoadGame.cpp:190-209</c>; retail delta at
    /// <c>CCareer__Load 0x00421200</c>).
    /// </summary>
    public int SuggestedWorldNumber { get; }

    public RetailCareerProgressionSummary ProgressionSummary => new(
        CareerInProgress,
        CompletedWorldCount,
        UnlockedGoodieCount,
        SuggestedWorldNumber);
}

/// <summary>
/// Deterministic, filesystem-free reader for the supported released-PC career
/// container. The caller supplies the bytes; this type never discovers, opens,
/// writes, or serializes a save.
/// </summary>
public static class RetailCareerSaveCodec
{
    public const ushort SupportedVersionWord = 0x4BD1;
    public const int SupportedContainerLength = 10_004;

    private const int CareerBase = 0x0002;
    private const int NodeArrayOffset = CareerBase + RetailCareerRecordLayout.NodeArrayOffset;
    private const int LinkArrayOffset = CareerBase + RetailCareerRecordLayout.LinkArrayOffset;
    private const int GoodieArrayOffset = CareerBase + RetailCareerRecordLayout.GoodieArrayOffset;
    private const int CareerInProgressOffset = CareerBase + RetailCareerRecordLayout.CareerInProgressOffset;

    /// <summary>
    /// Reads one supplied byte span. Stuart's non-PC path writes a 16-bit
    /// version followed by a raw <c>CCareer</c> block
    /// (<c>Career.cpp:1084-1163</c>); released PC retains that prefix while
    /// extending it with active option records and the 0x56-byte tail.
    /// </summary>
    public static RetailCareerSave Read(ReadOnlySpan<byte> source)
    {
        if (source.Length != SupportedContainerLength)
        {
            throw new RetailCareerSaveFormatException(
                $"Unsupported career length {source.Length}; expected 10,004 bytes.");
        }

        ushort versionWord = BinaryPrimitives.ReadUInt16LittleEndian(source);
        if (versionWord != SupportedVersionWord)
        {
            throw new RetailCareerSaveFormatException(
                $"Unsupported career version 0x{versionWord:X4}; expected 0x{SupportedVersionWord:X4}.");
        }

        ValidateCampaignLinks(source);

        var nodes = new RetailCareerReadNode[RetailWorldCatalog.NodeCount];
        for (int index = 0; index < nodes.Length; index++)
        {
            int offset = NodeArrayOffset + (index * RetailCareerRecordLayout.NodeStride);
            int worldNumber = ReadInt32(source, offset + 0x10);
            int expectedWorldNumber = RetailWorldCatalog.Nodes[index].WorldNumber;
            if (worldNumber != expectedWorldNumber)
            {
                throw new RetailCareerSaveFormatException(
                    $"Malformed campaign node {index}: world {worldNumber}, expected {expectedWorldNumber}.");
            }

            int lowerLink = ReadInt32(source, offset + 0x08);
            int higherLink = ReadInt32(source, offset + 0x0C);
            if (lowerLink != index * 2 || higherLink != (index * 2) + 1)
            {
                throw new RetailCareerSaveFormatException(
                    $"Malformed campaign node {index} link indices: {lowerLink}/{higherLink}.");
            }

            int complete = ReadInt32(source, offset + 0x04);
            float ranking = BitConverter.Int32BitsToSingle(ReadInt32(source, offset + 0x3C));
            byte grade = complete == 1
                ? RetailCareerGrade.GradeByteFromRanking(ranking)
                : RetailWorldGrade.IncompleteGradeByte;
            nodes[index] = new RetailCareerReadNode(
                index,
                worldNumber,
                complete,
                lowerLink,
                higherLink,
                ReadInt32(source, offset + 0x38),
                ranking,
                grade);
        }

        int unlockedGoodies = 0;
        for (int index = 0; index < RetailCareerRecordLayout.GoodieCount; index++)
        {
            if (ReadInt32(source, GoodieArrayOffset + (index * 4)) >= RetailCareerGoodieState.New)
            {
                unlockedGoodies++;
            }
        }

        var selectableWorlds = new List<int>(RetailWorldCatalog.NodeCount);
        foreach (RetailWorldNode node in RetailWorldCatalog.Nodes)
        {
            if (node.Index == 0 || HasCompleteIncomingLink(source, node.Index))
            {
                selectableWorlds.Add(node.WorldNumber);
            }
        }

        int suggestedWorld = selectableWorlds[^1];

        return new RetailCareerSave(
            source.ToArray(),
            versionWord,
            source.Length,
            ReadInt32(source, CareerInProgressOffset),
            Array.AsReadOnly(nodes),
            unlockedGoodies,
            selectableWorlds.AsReadOnly(),
            suggestedWorld);
    }

    private static bool HasCompleteIncomingLink(ReadOnlySpan<byte> source, int nodeIndex)
    {
        int usedLinkCount = RetailWorldCatalog.NodeCount * 2;
        for (int index = 0; index < usedLinkCount; index++)
        {
            int offset = LinkArrayOffset + (index * RetailCareerRecordLayout.LinkStride);
            int linkType = ReadInt32(source, offset);
            int toNode = ReadInt32(source, offset + 4);
            if (toNode == nodeIndex && linkType == RetailCareerNodeLink.Complete)
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateCampaignLinks(ReadOnlySpan<byte> source)
    {
        int usedLinkCount = RetailWorldCatalog.NodeCount * 2;
        for (int index = 0; index < usedLinkCount; index++)
        {
            int offset = LinkArrayOffset + (index * RetailCareerRecordLayout.LinkStride);
            int linkType = ReadInt32(source, offset);
            if (linkType is < RetailCareerNodeLink.NotComplete or > RetailCareerNodeLink.CompleteBroken)
            {
                throw new RetailCareerSaveFormatException(
                    $"Malformed campaign link state {linkType} at link {index}.");
            }

            RetailWorldNode owner = RetailWorldCatalog.Nodes[index / 2];
            int expectedToNode = (index & 1) == 0
                ? owner.LowerChildIndex
                : owner.HigherChildIndex;
            int toNode = ReadInt32(source, offset + 4);
            if (toNode != expectedToNode)
            {
                throw new RetailCareerSaveFormatException(
                    $"Malformed campaign link {index}: destination {toNode}, expected {expectedToNode}.");
            }
        }
    }

    private static int ReadInt32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset, 4));
}
