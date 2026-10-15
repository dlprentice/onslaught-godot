// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// Which delivered Level 100 character message is on screen at a given Core
/// tick, and how far into it the tick is - computed from Core ticks alone.
///
/// WHY THIS EXISTS. The HUD used to take both of those from
/// <see cref="Level100MessagePlaybackState"/>, i.e. from
/// <c>AudioStreamPlayer.GetPlaybackPosition()</c>. That is the audio mixer's
/// WALL CLOCK. Under <c>--fixed-fps 60</c> the engine frames a capture samples
/// are reproducible and the Core tick at each of them is reproducible, but the
/// mixer position at that frame is not: the mixer advances in real time while
/// the simulation advances as fast as the host allows. The capture rig keys its
/// shots on engine frames derived from Core ticks
/// (<c>FrontendCaptureRig.FrameForOffsetMs</c>), so the message on screen at a
/// pinned offset was a race against the host's speed.
///
/// MEASURED CONSEQUENCE. Two captures of the SAME commit disagreed by
/// 21.28 % material / meanD 16.5 on the message panel and 25.15 % / 5.0 on the
/// portrait/compass region, against retail's 0.02 % cross-run floor on the same
/// frame and 0.00 % on five other regions of ours
/// (local-lab/COMPASS-GAUGE-BLEND-2026-07-26.md section 4). Both regions are
/// driven by this clock: the type-on reveal and both
/// <c>FirstFlightHud.PortraitPoseIndex</c> and
/// <c>FirstFlightHud.MessageNoisePhaseIndex</c> hash the same position.
///
/// WHAT THIS IS NOT. It is not a schedule. Since the message-box law was
/// recovered (see <c>Level100MissionTiming.MessageBoxAllowedTick</c> and
/// <c>MessageAdvanceDelayTicks</c>), Core owns the schedule outright: it holds
/// each <c>Level100MessageRequested</c> back to the tick the message actually
/// becomes active, so the delivery tick IS the start tick and the audio adapter
/// and the HUD read the same instant. This type is the lookup over that stream.
///
/// WHY THE FULL TABLE VALUE IS THE DISPLAY DURATION. It was previously
/// <c>ExpectedPlaybackTicks - 18</c>, on the reading that the shipped table is a
/// script wait carrying a post-roll (it is the Ogg granule length plus 18.03
/// ticks on all 43 catalogued messages, which is not in dispute). That reading
/// is REFUTED as a display rule. Retail activates the message BEFORE the voice
/// starts - <c>CMessageBox__TryAdvanceQueuedMessage</c> (<c>0x004b7b80</c>)
/// waits 0.2 s before entering the voice/reveal path - and RETAINS it through
/// the 0.3 s completion hold of
/// <c>CMessageBox__AdvanceRevealAndScheduleNextTick</c> (<c>0x004b8020</c>).
/// Subtracting the 18 produced a voice-only window, i.e. the old
/// <c>AudioStreamPlayer.Playing</c> behaviour under a new name. The measured
/// retail spans in <c>rebuild/PROVENANCE.md</c> equal the table entries to
/// within the 50 ms sampler, and the gaps between them are a uniform six ticks.
/// </summary>
public static class Level100MessageSchedule
{
    /// <summary>
    /// How long a delivery is on screen, in Core ticks.
    /// </summary>
    public static int DisplayTicks(Level100HudMessageDeliverySnapshot delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        return Math.Max(1, delivery.ExpectedPlaybackTicks);
    }

    /// <summary>
    /// How many Core ticks before the end of a delivery's window the TEXT and
    /// the PORTRAIT stop being drawn. The panel BOX is not affected - it is
    /// drawn by <c>FirstFlightHud</c> whenever the message box is deployed,
    /// which is a different gate in retail and now a different gate here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE STRUCTURE IS PROVEN, THE EXACT LEAD IS BOUNDED. Retail's
    /// <c>CMessageBox__RenderOverlay</c> (<c>0x004b8850</c>) carries two
    /// independent draw gates in one function. The segmented meter bar - the
    /// panel box - is reached unless the deploy animator at <c>+0x2c4</c> has
    /// reached exactly 0, and that animator only retracts when there is no
    /// active message AND the queue at <c>+0x18</c> is empty, so it stays
    /// frozen fully open across an inter-message gap. The wrapped text block is
    /// gated separately on <c>+0x8 != 0</c> (the active <c>CMessage*</c>) and
    /// <c>+0x2c4 &gt;= 2.0</c>. There is no fade on the text: it is present on
    /// one frame and gone on the next, the frame <c>+0x8</c> is nulled by the
    /// <c>0xbba</c> completion event <c>CMessageBox__AdvanceRevealAndScheduleNextTick</c>
    /// (<c>0x004b8020</c>) schedules 0.30 s after the reveal completes.
    /// </para>
    /// <para>
    /// So retail has THREE panel states - typing, holding, and an empty box -
    /// and this reconstruction modelled two. The two frames that pinned it are
    /// <c>hud-timeline-run1/level100-t019074ms.png</c>, which draws the empty
    /// box in an inter-message gap where this client drew nothing, and
    /// <c>…/level100-t025065ms.png</c>, where retail has already cleared HUD_06's
    /// text and this client still held all three lines of it.
    /// </para>
    /// <para>
    /// THE VALUE IS FITTED TO A TWO-SIDED FRAME BOUND, NOT DERIVED. Against the
    /// retail message boundaries measured in <c>rebuild/PROVENANCE.md</c>, the
    /// reference frames put the text still on screen 196 ms before
    /// TUTORIAL_TECHNICIAN_01's end (<c>t033071</c>) and 188 ms before HUD_01's
    /// (<c>t011511</c>/<c>t011512</c>, one frame in each of two independent
    /// runs), and already gone 135 ms before HUD_06's (<c>t025065</c>). That is
    /// 4.05 &lt;= lead &lt; 5.64 Core ticks AT 30 Hz. Re-derived for the 20 Hz
    /// migration from the same two-sided millisecond bound - 135 ms &lt;= lead
    /// &lt; 188 ms - the window is 2.70 &lt;= lead &lt; 3.76 Core ticks, and 3
    /// is the only integer in it. The bound is a wall-clock measurement, so it
    /// re-derives rather than converts; note that 5 x 2/3 = 3.33 would have
    /// rounded to 3 as well, but only by luck.
    /// The static lane does NOT corroborate the value: decomposing the shipped
    /// 0.2 s promote-to-voice, 0.2 s post-audio and 0.3 s completion hold
    /// against the catalogued 18.03-tick table offset leaves a 3-tick lead, and
    /// 4 ticks of that decomposition are unaccounted for. Closing the gap needs
    /// the undisassembled 222-byte span <c>0x004b81c2</c>-<c>0x004b829f</c>,
    /// which holds the <c>0xbba</c> handler that actually nulls <c>+0x8</c>.
    /// Treat 5 as bounded measurement, not as a recovered constant.
    /// </para>
    /// </remarks>
    public const int MessageTextClearLeadTicks = 3;

    /// <summary>
    /// How long a delivery's TEXT and PORTRAIT are on screen, in Core ticks.
    /// The delivery's start tick and its full <see cref="DisplayTicks"/> window
    /// are untouched: this is a presentation lead off the END of the window, so
    /// the recovered message schedule - every start boundary and every six-tick
    /// gap - is unchanged.
    /// </summary>
    public static int VisibleTicks(Level100HudMessageDeliverySnapshot delivery) =>
        Math.Max(1, DisplayTicks(delivery) - MessageTextClearLeadTicks);

    /// <summary>
    /// The message on screen at <paramref name="tick"/>, or null in a gap.
    /// <paramref name="deliveries"/> is
    /// <c>Level100HudSnapshot.DeliveredMessages</c> in Core arrival order.
    /// </summary>
    public static Level100MessageScheduleEntry? ActiveAt(
        IReadOnlyList<Level100HudMessageDeliverySnapshot> deliveries,
        int tick)
    {
        ArgumentNullException.ThrowIfNull(deliveries);
        for (int index = 0; index < deliveries.Count; index++)
        {
            Level100HudMessageDeliverySnapshot delivery = deliveries[index];
            int start = delivery.Tick;
            int duration = DisplayTicks(delivery);
            // The containment test uses VisibleTicks; the entry still carries
            // the FULL duration, so the reveal clock and the portrait-pose
            // frame index keep the same zero point and the same denominator
            // they had before the text-clear lead existed.
            if (tick >= start && tick < start + VisibleTicks(delivery))
            {
                return new Level100MessageScheduleEntry(delivery, start, duration);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether retail's <c>CMessageBox</c> holds an active <c>CMessage*</c> at
    /// <c>+0x8</c>. This is NOT the same predicate as
    /// <see cref="ActiveAt(IReadOnlyList{Level100HudMessageDeliverySnapshot}, int)"/>,
    /// and the lower-right socket needs this one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>+0x8</c> is nulled by the <c>0xbba</c> completion event
    /// <see cref="MessageTextClearLeadTicks"/> ticks before a delivery's window
    /// ends, and is re-pointed by <c>CMessageBox__TryAdvanceQueuedMessage</c>
    /// (<c>0x004b7b80</c>) when the next queued message is promoted, 0.2 s
    /// before its reveal starts. Level 100's script queues from level start, so
    /// the promote happens at the previous window's end and <c>+0x8</c> is
    /// non-null right through the six-tick inter-message gap. The reveal has not
    /// begun, which is why the text rectangle is empty there.
    /// </para>
    /// <para>
    /// MEASURED, on four pinned retail frames whose socket disc was classified
    /// this session. The three frames the two-gate work identified as carrying
    /// zero glyph pixels in a gap - <c>hud-timeline-run1/t011756</c> (tick
    /// 352.7, HUD_01 ends 351), <c>t019074</c> (572.2, HUD_02 ends 567) and
    /// <c>t031058</c> (931.7, TUTORIAL_MESSAGE_LOG ends 926) - all draw the
    /// message-noise page in the socket. That page is drawn AFTER the
    /// <c>if (+0x8 == 0) return</c> at the head of
    /// <c>CMessageBox__RenderBattleLinePulseSprites</c> (<c>0x004b82b0</c>), so
    /// <c>+0x8</c> is non-null on all three. The fourth, <c>t025065</c> (751.9,
    /// HUD_06 ends 756, i.e. inside the clear lead), draws the influence overlay
    /// instead - and every draw inside <c>CDXBattleLine__Render</c> requires
    /// <c>+0x8 == 0</c>. The gap is held; the lead is not.
    /// </para>
    /// <para>
    /// The portraits are absent on those three gap frames while the noise is
    /// present, which the same function explains with a second gate: its six
    /// portrait draws sit inside <c>if (*(int *)(this + 0x24) == 0)</c>. That
    /// field is not modelled, and it does not need to be here: the client's
    /// speaker and pose come from
    /// <see cref="ActiveAt(IReadOnlyList{Level100HudMessageDeliverySnapshot}, int)"/>,
    /// which is already null through the gap, so the socket draws the noise page
    /// alone there - which is what retail draws. Whether the two predicates stay
    /// aligned outside Level 100 is untested.
    /// </para>
    /// </remarks>
    public static bool MessageBoxHoldsActiveMessage(
        IReadOnlyList<Level100HudMessageDeliverySnapshot> deliveries,
        int tick)
    {
        ArgumentNullException.ThrowIfNull(deliveries);
        for (int index = 0; index < deliveries.Count; index++)
        {
            Level100HudMessageDeliverySnapshot delivery = deliveries[index];
            int start = delivery.Tick;
            if (tick >= start && tick < start + VisibleTicks(delivery))
            {
                return true;
            }

            // The promote window, which is exactly Core's advance delay - the
            // released 0.2 s of CMessageBox__TryAdvanceQueuedMessage - off the
            // end of this delivery's window. It is deliberately NOT expressed
            // as "until the next delivery's tick": Core appends a delivery when
            // it becomes active, so at any tick inside the gap the next
            // delivery is not in this list yet and that formulation collapses
            // to false on every real frame. That defect was caught by a capture
            // pair that came back byte-identical, not by these types.
            int end = start + DisplayTicks(delivery);
            if (tick >= end && tick < end + Level100MissionTiming.MessageAdvanceDelayTicks)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// One scheduled message: which delivery, the Core tick it starts on, and how
/// many Core ticks it runs for.
/// </summary>
public readonly record struct Level100MessageScheduleEntry(
    Level100HudMessageDeliverySnapshot Delivery,
    int StartTick,
    int DurationTicks)
{
    public int ElapsedTicksAt(int tick) =>
        Math.Clamp(tick - StartTick, 0, DurationTicks);

    public double ElapsedSecondsAt(int tick) =>
        ElapsedTicksAt(tick) / (double)SimulationConstants.TicksPerSecond;

    public double DurationSeconds =>
        DurationTicks / (double)SimulationConstants.TicksPerSecond;
}
