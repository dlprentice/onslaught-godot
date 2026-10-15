// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// The client half of <c>BUTTON_SKIP_PANNING</c> (<c>0x3a</c>): the edge seam
/// and the released key bindings.
/// </summary>
public sealed class Level100SkipPanningClientTests
{
    private const uint Seed = 0x4F4E534Cu;
    private const long OneCoreStepTicks = 500_000;

    private static Level100ActorDefinitionSet ActorDefinitions =>
        Level100TestActorDefinitions.Create();

    [Fact]
    public void QueuedSkip_EndsTheOpeningPanOnTheNextStep()
    {
        var session = new InteractiveSession(Seed, ActorDefinitions);

        for (int step = 0; step < 5; step++)
        {
            session.AdvanceFrameTicks(OneCoreStepTicks);
        }

        Assert.Equal(
            SimulationConstants.Level100OpeningPanTicks - 5,
            session.CurrentSnapshot.Level100OpeningTicksRemaining);

        session.QueueSkipPanning();
        Assert.True(session.HasHeldOrPendingInput);

        FrameAdvanceResult skipped = session.AdvanceFrameTicks(OneCoreStepTicks);

        Assert.Equal(0, skipped.CurrentSnapshot.Level100OpeningTicksRemaining);
        // The gate is on the mission clock, which counts the pre-run.
        Assert.Equal(
            skipped.CurrentSnapshot.Level100Mission.Tick + Level100MissionTiming.ReleasedEventFrameTicks,
            skipped.CurrentSnapshot.Level100Mission.MessageBoxAllowedTick);
        Assert.False(session.HasHeldOrPendingInput);
    }

    [Fact]
    public void SkipIsOneEdge_NotAHeldLevel()
    {
        // Every shipped BUTTON_SKIP_PANNING row is push type 8 (KEY_ONCE), so
        // the queued edge must be consumed by exactly one step. A run that
        // queues once and then coasts must agree, tick for tick, with a run
        // that steps straight through the same skip.
        var queued = new InteractiveSession(Seed, ActorDefinitions);
        var direct = new Simulation(Seed, ActorDefinitions);

        queued.QueueSkipPanning();
        for (int step = 0; step < 40; step++)
        {
            queued.AdvanceFrameTicks(OneCoreStepTicks);
            direct.Step(step == 0
                ? new SimInput(0, 0, SimActions.SkipPanning)
                : SimInput.Idle);
        }

        Assert.Equal(
            StateHasher.ComputeHex(direct.Snapshot),
            StateHasher.ComputeHex(queued.CurrentSnapshot));
    }

    [Fact]
    public void SkipQueuedWhilePausedOrSuspended_IsDropped()
    {
        var session = new InteractiveSession(Seed, ActorDefinitions);
        session.SetAuthenticMenuPaused(true);
        session.QueueSkipPanning();
        session.SetAuthenticMenuPaused(false);

        // The pause boundary clears held and pending input and requires a
        // neutral sample before anything is accepted again, so the skip queued
        // behind the menu must not leak into the resumed session.
        Assert.False(session.HasHeldOrPendingInput);
        session.ObserveInput(InteractiveInput.Idle);
        FrameAdvanceResult result = session.AdvanceFrameTicks(OneCoreStepTicks);
        Assert.Equal(
            SimulationConstants.Level100OpeningPanTicks - 1,
            result.CurrentSnapshot.Level100OpeningTicksRemaining);
    }

    /// <summary>
    /// Which keys the Godot client binds to the skip — pinned as a
    /// RECONSTRUCTION CHOICE, not as proven released reachability.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>RESCOPED 2026-07-27.</b> This test was called
    /// <c>GodotInputBindsTheReleasedSkipPanningKeys</c> and read as though
    /// Space, Enter and Numpad Enter were established to reach
    /// <c>BUTTON_SKIP_PANNING</c> in retail. They are not, and neither is
    /// Escape. An adversarial review named the asymmetry: the client excludes
    /// Escape because <c>GetKeyOnce</c> consumption is unresolved, and the
    /// identical unresolved question applies to the other three.
    /// </para>
    /// <para>
    /// What IS proven and is asserted below: the shipped 47-row table
    /// (<c>OptionsEntries__InitDefaultSingleBindingsTable</c>,
    /// <c>0x00514210</c>) gives <c>0x3a</c> four hard-wired KEY_ONCE rows at
    /// indices 22-25 with DIK scan codes <c>0x39</c>, <c>0x1c</c>, <c>0x01</c>
    /// and <c>0x9c</c>; and this client binds three of them and leaves Escape
    /// to the authentic pause menu, which is the same key's other shipped
    /// meaning (row 34, <c>BUTTON_PAUSE</c>).
    /// </para>
    /// <para>
    /// What is NOT proven, and is deliberately not asserted anywhere: that a
    /// press of ANY of the four reaches row 22-25 in the released build. Every
    /// one of the four is also bound to an earlier row — Space to row 9
    /// <c>BUTTON_MECH_MORPH</c>, Enter and Numpad Enter to row 20
    /// <c>BUTTON_SKIP_CUTSCENE</c>, Escape to rows 17/20/34 — and
    /// <c>references/Onslaught/ltshell.h:292</c> shows the PC shell's
    /// <c>xKeyOnce</c> CONSUMING the flag it reads. Retail's own
    /// <c>GetKeyOnce</c> body is absent from the partial drop.
    /// <c>references/Onslaught/PCController.cpp:76</c> binds
    /// <c>BUTTON_SKIP_PANNING</c> as <c>BUTTON_ONCE, 1</c> — a PAD button, not
    /// a key — which deepens the doubt rather than resolving it. The full
    /// statement lives on <c>SimActions.SkipPanning</c>.
    /// </para>
    /// <para>
    /// So this test pins a decision the reconstruction made, and will have to be
    /// revisited — not merely re-run — if the routing is ever settled from
    /// bytes. It must not be read as evidence that retail skips on Space.
    /// </para>
    /// </remarks>
    [Fact]
    public void GodotBindsThreeOfTheFourShippedSkipScanCodes_ReachabilityUnproven()
    {
        string game = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "FirstFlightGame.cs"));
        string input = ExtractMethod(game, "public override void _Input(InputEvent inputEvent)");

        Assert.Contains("_session.QueueSkipPanning();", input, StringComparison.Ordinal);
        int skipIndex = input.IndexOf("_session.QueueSkipPanning();", StringComparison.Ordinal);
        string guard = input[..skipIndex];
        int guardStart = guard.LastIndexOf("if (", StringComparison.Ordinal);
        string binding = guard[guardStart..];

        // The three the client chose. This is the reconstruction's decision,
        // pinned so it cannot drift silently - NOT a claim that retail's row 22
        // ever sees these presses. See the remarks.
        Assert.Contains("Key.Space", binding, StringComparison.Ordinal);
        Assert.Contains("Key.Enter", binding, StringComparison.Ordinal);
        Assert.Contains("Key.KpEnter", binding, StringComparison.Ordinal);
        Assert.DoesNotContain("Key.Escape", binding, StringComparison.Ordinal);

        // Escape stays with the authentic pause menu, which is the same key's
        // other shipped meaning (row 34, BUTTON_PAUSE).
        Assert.Contains("Key.Escape", input, StringComparison.Ordinal);
        Assert.Contains("BUTTON_SKIP_PANNING", game, StringComparison.Ordinal);
        Assert.Contains("0x00514210", game, StringComparison.Ordinal);

        // And the unresolved routing has to stay written down next to the
        // binding. Deleting the explanation is how an unproven choice becomes an
        // assumed fact - which is exactly what happened to the three keys above
        // while Escape kept its reasoning.
        Assert.Contains("GetKeyOnce", game, StringComparison.Ordinal);
        Assert.Contains("ltshell.h:292", game, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string signature)
    {
        int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Missing method signature: {signature}");
        int openingBrace = source.IndexOf('{', signatureIndex);
        Assert.True(openingBrace >= 0, $"Missing method body: {signature}");

        int depth = 0;
        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return source[(openingBrace + 1)..index];
            }
        }

        throw new InvalidOperationException($"Unterminated method body: {signature}");
    }
}
