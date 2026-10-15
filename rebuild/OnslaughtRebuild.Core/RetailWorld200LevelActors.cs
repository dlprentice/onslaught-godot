// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Measured census of world 200's level-world (RLWD) initial actors and its
/// own base world. Not a session owner.
/// </summary>
/// <remarks>
/// <para>
/// Measured 2026-08-22 from <c>data/resources/200_res_PC.aya</c> (archive
/// SHA-256 <c>99dbd433…b77</c>). After the 14 admitted script objects the
/// RLWD actor header is <c>(3, 0, 54)</c> — Level 100's is <c>(1, 0, 45)</c>
/// and world 110's is <c>(2, 0, 40)</c>. The header's post-zeros word is 2
/// where both earlier worlds carry 1. The HFLD envelope is inside ERES just
/// like worlds 100 and 110, and its payload is the same 668,652-byte law the
/// Core loader pins; packaging is not a world-200 divergence.
/// </para>
/// <para>
/// The BSWD chunk is world 200's own 80,232-byte base world (SHA-256
/// <c>9c0575ea…adba</c>) — NOT the byte-identical island world 110 shares
/// with Level 100 (54,669 B, <c>04c5a383…10f4</c>). World 200 is a different
/// island as well as a different overlay.
/// </para>
/// <para>
/// <b>What this deliberately does not claim.</b> No per-type actor counts
/// are pinned yet: the record walk past the 54-record header was not
/// completed against the L100 reader, whose type-8/15/18/27/36/37 trailer
/// law is Level 100's. World 200's first record is a type-15 row and its
/// records carry per-record string pairs (e.g.
/// <c>LandingCraftAlpha</c>) where world 110's carried none, so the trailer
/// law itself needs its own measurement before any count is promoted.
/// </para>
/// </remarks>
public static class RetailWorld200LevelActors
{
    public const int InitialActorCount = 54;

    public const int ActorHeaderA = 3;

    public const int ActorHeaderB = 0;

    /// <summary>The level-world header's post-zeros word: 2, not 1.</summary>
    public const int HeaderPostZerosWord = 2;

    /// <summary>SHA-256 of the framed HFLD envelope extracted from ERES.</summary>
    public const string HeightfieldEnvelopeSha256 =
        "1B8EB8584BE552383F10B08C75D9F10E91708343F0E5EE085D5130D369F6B945";

    public const int HeightfieldEnvelopeBytes = 668_660;

    /// <summary>SHA-256 of world 200's own BSWD base world.</summary>
    public const string BaseWorldSha256 =
        "9C0575EAF43AD852F74CA71F931548B2FDC4025163656105484F2A030070ADBA";

    public const int BaseWorldBytes = 80_232;

    public const int ScriptObjectCount = 14;
}
