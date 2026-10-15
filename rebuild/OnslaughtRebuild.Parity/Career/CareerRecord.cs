// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/Career.h and src/Career.cpp (CCareer, CCareerNode, CCareerNodeLink, CGoodie).

using System.Buffers.Binary;

namespace OnslaughtRebuild.Parity.Career;

/// <summary>
/// The game's <c>CCareer</c> object as its 9,404 bytes of memory. The career is saved and loaded as a raw copy of
/// this memory (<c>CCareer::Save</c> 0x00421350, <c>CCareer::Load</c> 0x00421200), and <c>CCareer::Blank</c> leaves
/// fields it does not reset as they were, so the record is kept as bytes and every field is read and written in
/// place. The layout has no padding: every field is a 4-byte integer, float or enum.
/// </summary>
public sealed class CareerRecord
{
    public const int Size = 9_404;
    public const int NodeCount = 100;
    public const int NodeSize = 64;
    public const int LinkCount = 200;
    public const int LinkSize = 8;
    public const int GoodieCount = 300;
    public const int KillTypeCount = 5;
    public const int SlotWordCount = 32;
    public const int BaseThingWords = 9;

    // Offsets measured from an MSVC-layout dump of the decomp's Career.h.
    private const int PendingExtraGoodiesOffset = 0;
    private const int NodesOffset = 4;
    private const int LinksOffset = 6_404;
    private const int GoodiesOffset = 8_004;
    private const int KilledThingsOffset = 9_204;
    private const int SlotsOffset = 9_224;
    private const int CareerInProgressOffset = 9_352;
    private const int SoundVolumeOffset = 9_356;
    private const int MusicVolumeOffset = 9_360;
    private const int IsGodOffset = 9_364;
    private const int InvertJetYAxisOffset = 9_372;
    private const int InvertYAxisOffset = 9_380;
    private const int VibrationOffset = 9_388;
    private const int ControllerConfigurationOffset = 9_396;

    // CCareerNode: mIsStartOfNewIsland, mComplete, mLowerLink, mHigherLink, mWorldNumber, mBaseThingsExists[9],
    // mNumAttempts, mRanking.
    private const int NodeIsStartOfNewIslandAt = 0;
    private const int NodeCompleteAt = 4;
    private const int NodeLowerLinkAt = 8;
    private const int NodeHigherLinkAt = 12;
    private const int NodeWorldNumberAt = 16;
    private const int NodeBaseThingsAt = 20;
    private const int NodeNumAttemptsAt = 56;
    private const int NodeRankingAt = 60;

    private readonly byte[] _bytes;

    private CareerRecord(byte[] bytes) => _bytes = bytes;

    /// <summary>A record holding a copy of <paramref name="bytes"/>, which must be exactly <see cref="Size"/> long.</summary>
    public static CareerRecord FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size)
            throw new ArgumentException($"A career record is {Size} bytes, not {bytes.Length}.", nameof(bytes));
        return new CareerRecord(bytes.ToArray());
    }

    /// <summary>
    /// The record as the game's global <c>CAREER</c> holds it after static construction: the node and link
    /// constructors run (<c>CCareerNode::Blank</c> and the link's initialiser), goodies are unknown, the volumes
    /// are 0.8 and 0.9, controller configuration 1, vibration on, and everything else is the zero of static storage.
    /// </summary>
    public static CareerRecord Constructed()
    {
        CareerRecord record = new(new byte[Size]);
        for (int node = 0; node < NodeCount; node++) record.BlankNode(node);
        for (int link = 0; link < LinkCount; link++)
        {
            record.SetLinkType(link, CareerLinkType.NotComplete);
            record.SetLinkToNode(link, -1);
        }
        record.CareerInProgress = 0;
        record.SoundVolume = 0.8f;
        record.MusicVolume = 0.9f;
        for (int player = 0; player < 2; player++)
        {
            record.SetControllerConfigurationNum(player, 1);
            record.SetInvertYAxis(player, 0);
            record.SetInvertJetYAxis(player, 0);
            record.SetVibration(player, 1);
        }
        return record;
    }

    public CareerRecord Clone() => new((byte[])_bytes.Clone());

    public ReadOnlySpan<byte> Bytes => _bytes;

    /// <summary>The raw memory copy the game's <c>memcpy(this, source, sizeof(CCareer))</c> performs.</summary>
    public void CopyFrom(ReadOnlySpan<byte> source)
    {
        if (source.Length < Size) throw new ArgumentException("The source is shorter than a career record.", nameof(source));
        source[..Size].CopyTo(_bytes);
    }

    public int PendingExtraGoodies { get => I(PendingExtraGoodiesOffset); set => I(PendingExtraGoodiesOffset, value); }
    public int CareerInProgress { get => I(CareerInProgressOffset); set => I(CareerInProgressOffset, value); }
    public float SoundVolume { get => F(SoundVolumeOffset); set => F(SoundVolumeOffset, value); }
    public float MusicVolume { get => F(MusicVolumeOffset); set => F(MusicVolumeOffset, value); }

    public int NodeIsStartOfNewIsland(int node) => I(Node(node) + NodeIsStartOfNewIslandAt);
    public int NodeCompleteFlag(int node) => I(Node(node) + NodeCompleteAt);
    public void SetNodeComplete(int node, int value) => I(Node(node) + NodeCompleteAt, value);
    public int NodeLowerLinkIndex(int node) => I(Node(node) + NodeLowerLinkAt);
    public void SetNodeLowerLink(int node, int value) => I(Node(node) + NodeLowerLinkAt, value);
    public int NodeHigherLinkIndex(int node) => I(Node(node) + NodeHigherLinkAt);
    public void SetNodeHigherLink(int node, int value) => I(Node(node) + NodeHigherLinkAt, value);
    public int NodeWorldNumber(int node) => I(Node(node) + NodeWorldNumberAt);
    public void SetNodeWorldNumber(int node, int value) => I(Node(node) + NodeWorldNumberAt, value);
    public int NodeBaseThingsWord(int node, int word) => I(Node(node) + NodeBaseThingsAt + 4 * CheckIndex(word, BaseThingWords));
    public void SetNodeBaseThingsWord(int node, int word, int value) => I(Node(node) + NodeBaseThingsAt + 4 * CheckIndex(word, BaseThingWords), value);
    public int NodeNumAttempts(int node) => I(Node(node) + NodeNumAttemptsAt);
    public void SetNodeNumAttempts(int node, int value) => I(Node(node) + NodeNumAttemptsAt, value);
    public float NodeRanking(int node) => F(Node(node) + NodeRankingAt);
    public void SetNodeRanking(int node, float value) => F(Node(node) + NodeRankingAt, value);

    public CareerLinkType LinkType(int link) => (CareerLinkType)I(Link(link));
    public void SetLinkType(int link, CareerLinkType value) => I(Link(link), (int)value);
    public int LinkToNode(int link) => I(Link(link) + 4);
    public void SetLinkToNode(int link, int value) => I(Link(link) + 4, value);

    public CareerGoodieState GoodieState(int goodie) => (CareerGoodieState)I(GoodiesOffset + 4 * CheckIndex(goodie, GoodieCount));
    public void SetGoodieState(int goodie, CareerGoodieState value) => I(GoodiesOffset + 4 * CheckIndex(goodie, GoodieCount), (int)value);

    public int KilledThings(int type) => I(KilledThingsOffset + 4 * CheckIndex(type, KillTypeCount));
    public void SetKilledThings(int type, int value) => I(KilledThingsOffset + 4 * CheckIndex(type, KillTypeCount), value);

    public int SlotWord(int word) => I(SlotsOffset + 4 * CheckIndex(word, SlotWordCount));
    public void SetSlotWord(int word, int value) => I(SlotsOffset + 4 * CheckIndex(word, SlotWordCount), value);

    public int IsGod(int player) => I(IsGodOffset + 4 * CheckIndex(player, 2));
    public void SetIsGod(int player, int value) => I(IsGodOffset + 4 * CheckIndex(player, 2), value);
    public int InvertJetYAxis(int player) => I(InvertJetYAxisOffset + 4 * CheckIndex(player, 2));
    public void SetInvertJetYAxis(int player, int value) => I(InvertJetYAxisOffset + 4 * CheckIndex(player, 2), value);
    public int InvertYAxis(int player) => I(InvertYAxisOffset + 4 * CheckIndex(player, 2));
    public void SetInvertYAxis(int player, int value) => I(InvertYAxisOffset + 4 * CheckIndex(player, 2), value);
    public int Vibration(int player) => I(VibrationOffset + 4 * CheckIndex(player, 2));
    public void SetVibration(int player, int value) => I(VibrationOffset + 4 * CheckIndex(player, 2), value);
    public int ControllerConfigurationNum(int player) => I(ControllerConfigurationOffset + 4 * CheckIndex(player, 2));
    public void SetControllerConfigurationNum(int player, int value) => I(ControllerConfigurationOffset + 4 * CheckIndex(player, 2), value);

    /// <summary><c>CCareerNode::Blank</c> (0x0041b740): mIsStartOfNewIsland is left as it is.</summary>
    public void BlankNode(int node)
    {
        SetNodeLowerLink(node, -1);
        SetNodeHigherLink(node, -1);
        SetNodeWorldNumber(node, 0);
        SetNodeComplete(node, 0);
        SetNodeNumAttempts(node, 0);
        for (int word = 0; word < BaseThingWords; word++) SetNodeBaseThingsWord(node, word, unchecked((int)0xffffffff));
        SetNodeRanking(node, -1f);
    }

    private static int Node(int node) => NodesOffset + NodeSize * CheckIndex(node, NodeCount);
    private static int Link(int link) => LinksOffset + LinkSize * CheckIndex(link, LinkCount);

    private static int CheckIndex(int index, int count)
    {
        if ((uint)index >= (uint)count) throw new ArgumentOutOfRangeException(nameof(index), index, $"0..{count - 1}");
        return index;
    }

    private int I(int offset) => BinaryPrimitives.ReadInt32LittleEndian(_bytes.AsSpan(offset));
    private void I(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(_bytes.AsSpan(offset), value);
    private float F(int offset) => BinaryPrimitives.ReadSingleLittleEndian(_bytes.AsSpan(offset));
    private void F(int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(_bytes.AsSpan(offset), value);
}

/// <summary><c>ECNLinkType</c>.</summary>
public enum CareerLinkType
{
    NotComplete = 0,
    Complete = 1,
    CompleteBroken = 2,
}

/// <summary><c>EGoodieState</c>.</summary>
public enum CareerGoodieState
{
    Unknown = 0,
    Instructions = 1,
    New = 2,
    Old = 3,
}
