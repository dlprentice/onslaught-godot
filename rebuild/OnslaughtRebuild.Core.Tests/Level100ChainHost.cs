// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// What <see cref="Level100ChainAutopilot"/> drives.
///
/// <para>The autopilot decides in <see cref="SimInput"/> and reads
/// <see cref="WorldSnapshot"/>; it has no opinion about who owns the
/// simulation. This seam exists so the same decisions can be delivered either
/// straight into <see cref="Simulation"/> - which is what every measurement
/// before 2026-07-27 did - or through
/// <see cref="OnslaughtRebuild.Client.InteractiveSession"/>, which is the
/// surface the shipping client actually gives a player.</para>
///
/// <para><b>It is not an abstraction over the simulation.</b> There is exactly
/// one <see cref="Simulation"/> underneath either implementation. What differs
/// is the route the input takes to reach it.</para>
/// </summary>
internal interface ILevel100ChainHost
{
    WorldSnapshot Snapshot { get; }

    /// <summary>
    /// Exact successful actor-round damage identities for the just-completed
    /// step, when this host can expose Core's internal causal receipt. A null
    /// value means hit outcome is unknown rather than a miss.
    /// </summary>
    IReadOnlyList<int>? ActorRoundDamageIdsForMeasurement { get; }

    WorldSnapshot Step(SimInput input);
}

/// <summary>
/// One member of the twenty-point one-permille perturbation sweep this suite
/// measures chain outcomes over.
///
/// <para><b>The shape is the one written down on
/// <c>Level100ChainAutopilot</c>'s class remarks</b> (see
/// <c>PrecisionStandOffMillimeters</c>): "twenty +-1 permille perturbations of
/// every look command at or above thresholds of 100 to 1000 - the
/// generalisation of the 'one permille shaved off every command &gt;= 500'
/// control". Ten thresholds, two signs, twenty runs. It was described in prose
/// and re-derived by hand every time somebody needed it; it is a type here so
/// that a sweep result is reproducible rather than quoted.</para>
///
/// <para><b>The SCOPE is beat 9 and only beat 9</b>, which is the sweep
/// <c>Level100FullChainTests</c> describes at "twenty separate one-permille
/// perturbations of beat 9 ALONE". Applied to the whole run instead, the same
/// twenty perturbations break beat 3 outright in eight of them - the driver
/// loses <c>TutorialBroken</c> at t3776 and never reaches a flight leg at all,
/// which makes it a useless instrument for a question about the last leg.
/// Measured, and the reason the scope is stated here rather than left
/// implied.</para>
///
/// <para><b>Why a permille and not something bigger.</b> One permille of stick
/// is far below what the client's pointer path can even deliver - the reachable
/// lattice starts at 30 - so this is not a model of a worse player. It is a
/// determinism probe: the chain outcome is chaotic at this scale and always
/// was, so a policy that only survives the unperturbed trajectory has not been
/// shown to survive anything.</para>
/// </summary>
internal readonly record struct Level100LookPerturbation(
    int ThresholdPermille,
    int DeltaPermille)
{
    /// <summary>The twenty perturbations, in a fixed order.</summary>
    internal static IReadOnlyList<Level100LookPerturbation> Sweep { get; } =
        Enumerable.Range(1, 10)
            .SelectMany(step => new[]
            {
                new Level100LookPerturbation(step * 100, -1),
                new Level100LookPerturbation(step * 100, +1),
            })
            .ToArray();

    /// <summary>
    /// Nudges the MAGNITUDE of a look command by one permille, sign preserved,
    /// when the command is at or above this perturbation's threshold. Commands
    /// below the threshold, and the zero command, pass through untouched.
    /// </summary>
    internal short Apply(short requested)
    {
        int magnitude = Math.Abs((int)requested);
        if (magnitude < ThresholdPermille)
        {
            return requested;
        }

        int perturbed = Math.Clamp(magnitude + DeltaPermille, 0, 1_000);
        return (short)(requested < 0 ? -perturbed : perturbed);
    }

    public override string ToString() =>
        $"|look|>={ThresholdPermille} {(DeltaPermille < 0 ? "-" : "+")}1";
}

/// <summary>The pre-existing route: straight into Core.</summary>
internal sealed class Level100DirectChainHost : ILevel100ChainHost
{
    private readonly Simulation _simulation;
    private readonly bool _quantizeLookToClientPointerPath;
    private readonly bool _quantizeLookToIntegerMousePixels;

    /// <param name="quantizeLookToClientPointerPath">
    /// Apply only the analogue-look quantisation the client's pointer path
    /// imposes, and change nothing else.
    ///
    /// <para>This is an INSTRUMENT CHECK, not a mode. If a run with this set
    /// produces the same pose trace as the same run through
    /// <see cref="Level100InteractiveChainHost"/>, then that host's inverse of
    /// the pointer laws is exact and the pixel quantum is the ONLY thing the
    /// client costs. Without it, "the client is lossy" and "this driver
    /// mistranslates" are the same observation.</para>
    /// </param>
    /// <param name="quantizeLookToIntegerMousePixels">
    /// Restrict the analogue look axis to the positions a hand on a mouse can
    /// actually reach. See
    /// <see cref="Level100InteractiveChainHost.PlayerPlausiblePermille"/>.
    ///
    /// <para>This constrains the driver's OUTPUT channel, and only that. With
    /// it set the driver is no longer allowed sub-pixel aim: every stick
    /// position it asks for snaps to one a whole retail mouse pixel can
    /// produce, so a beat it then fails is a beat a player would also have to
    /// fight for <em>with the same hand</em>.</para>
    ///
    /// <para><b>It does not make the run player-plausible.</b> The PERCEPTION
    /// channel is still omniscient: this driver reads exact
    /// <c>Health</c>, full three-dimensional actor poses, and sampled
    /// line-of-sight ray tests against terrain. A player has a compass, a
    /// scanner and a windscreen. <c>GOAL.md</c> demotes the driven <c>Won</c>
    /// run to an acceptance test precisely because "an autopilot that reaches
    /// <c>Won</c> by means no player could reproduce has proved nothing";
    /// integer mouse pixels close one half of that gap and the wording here
    /// used to claim the whole of it.</para>
    /// </param>
    internal Level100DirectChainHost(
        Simulation simulation,
        bool quantizeLookToClientPointerPath = false,
        bool quantizeLookToIntegerMousePixels = false)
    {
        _simulation = simulation;
        _quantizeLookToClientPointerPath = quantizeLookToClientPointerPath;
        _quantizeLookToIntegerMousePixels = quantizeLookToIntegerMousePixels;
    }

    public WorldSnapshot Snapshot => _simulation.Snapshot;

    public IReadOnlyList<int>? ActorRoundDamageIdsForMeasurement =>
        _simulation.ActorRoundDamageIdsForMeasurement;

    public WorldSnapshot Step(SimInput input)
    {
        if (_quantizeLookToIntegerMousePixels)
        {
            input = input with
            {
                LookXAnalogPermille =
                    Level100InteractiveChainHost.PlayerPlausiblePermille(
                        input.LookXAnalogPermille),
                LookYAnalogPermille =
                    Level100InteractiveChainHost.PlayerPlausiblePermille(
                        input.LookYAnalogPermille),
            };
        }

        if (_quantizeLookToClientPointerPath)
        {
            input = input with
            {
                LookXAnalogPermille =
                    Level100InteractiveChainHost.DeliverablePermille(
                        input.LookXAnalogPermille),
                LookYAnalogPermille =
                    Level100InteractiveChainHost.DeliverablePermille(
                        input.LookYAnalogPermille),
            };
        }

        return _simulation.Step(input);
    }
}

/// <summary>
/// The client's route: every decision is delivered through the same
/// <see cref="InteractiveSession"/> methods the Godot client calls from
/// <c>_Process</c> and <c>_Input</c>, and the simulation is advanced by handing
/// the session a frame time rather than by calling <c>Step</c>.
///
/// <para><b>Three of the autopilot's channels do not survive the trip
/// unchanged, and that is the point of running it.</b></para>
///
/// <list type="number">
///   <item><description><b>Held state</b> - <c>MoveX</c>, <c>MoveZ</c>,
///   <c>LandingJets</c>, digital <c>LookX/LookY</c> - goes through
///   <see cref="InteractiveSession.ObserveInput"/> exactly as
///   <c>FirstFlightGame.SampleInput</c> polls it. Lossless.</description></item>
///   <item><description><b>Discrete edges</b> - <c>Fire</c>,
///   <c>ToggleMode</c>, <c>Reset</c>, and <c>ChangeWeapon</c> - go through
///   <see cref="InteractiveSession.QueueFirePulse"/>,
///   <see cref="InteractiveSession.QueueToggleMode"/>,
///   <see cref="InteractiveSession.QueueReset"/>, and
///   <see cref="InteractiveSession.QueueChangeWeapon"/>, which is the key-press
///   or already-sampled release path the client uses. Lossless, and
///   deliberately not a physical button level: <c>SimActions.Fire</c> already
///   means retail's <c>BUTTON_RELEASE</c> semantic edge.</description></item>
///   <item><description><b>The analogue look axis is LOSSY, and the loss is
///   measured rather than assumed.</b> The autopilot commands a stick position
///   in permille. The client has no route for that: <c>SampleInput</c> leaves
///   the look actions unbound on purpose
///   (<c>FirstFlightGame.ConfigureInputMap</c>), so the only way to move the
///   analogue axis is mouse motion through
///   <see cref="InteractiveSession.QueuePointerMotionMilliPixels"/>. That path
///   is quantised to the whole pixels retail's cursor is made of, so the
///   reachable axis is the lattice <c>{0, ±30, ±61, ±91, …}</c> and
///   <b>everything strictly between two lattice points is unreachable</b>. That
///   is retail's own quantum, not an adapter defect: until 2026-07-30 this path
///   also carried an invented half-pixel dead zone, which is gone. See
///   <see cref="UnreachableLookCommands"/>.
///   </description></item>
/// </list>
/// </summary>
internal sealed class Level100InteractiveChainHost : ILevel100ChainHost
{
    /// <summary>
    /// Frame time the client pins itself to when it is being driven
    /// deterministically (<c>FirstFlightGame.SmokeFrameElapsedTicks</c>).
    ///
    /// <para>30 Hz does not divide <see cref="TimeSpan.TicksPerSecond"/>, so
    /// this is the client's own rounded-up value, not a new one. It advances
    /// exactly one simulation step per frame and leaves 20 phase units of
    /// residue; <see cref="InteractiveSession.PhaseUnitsPerStep"/> is 10,000,000,
    /// so the first double-step would arrive at frame 500,000. This run is four
    /// orders of magnitude short of that, and <see cref="Step"/> asserts the
    /// one-step property on every frame rather than trusting the arithmetic.
    /// </para>
    /// </summary>
    internal const long FrameElapsedTicks = 500_000;

    // The inverse of InteractiveSession's two published pointer laws. Both
    // constants are transcribed from OnslaughtRebuild.Client/InteractiveSession.cs
    // (PointerAxisNumerator/Denominator and PointerOffsetRetention*), which
    // carries their retail provenance: 91/3000 is the shipped 13/3000 scalar at
    // pristine VA 0x005d97c8 times the image's untouched 7.0 sensitivity, and
    // and 10/17 is retail's own recentring, used verbatim since Core moved to
    // retail's 20 Hz (it read 702049/1000000, the 30 Hz time-equivalent).
    private const int PointerAxisNumerator = 91;
    private const int PointerAxisDenominator = 3_000;
    private const int PointerRetentionNumerator = 10;
    private const int PointerRetentionDenominator = 17;

    /// <summary>
    /// Milli-pixels in one whole pixel - retail's cursor quantum, and since
    /// 2026-07-30 the only thing standing between the driver and the axis.
    ///
    /// <para>This replaced a <c>PointerDeadZoneMilliPixels = 500</c> that
    /// mirrored an invented half-pixel floor in the client. Retail has no dead
    /// zone on this path at all; see <c>InteractiveSession.WholePixelsOf</c>
    /// for the three constants and the absence of 0.36 from the shipped
    /// image.</para>
    /// </summary>
    private const int PointerPixelMilliPixels = 1_000;

    private readonly InteractiveSession _session;
    private readonly bool _quantizeLookToIntegerMousePixels;

    /// <summary>
    /// What the session's stored pointer offset is after the last step.
    ///
    /// <para>It is not read back from the session, because the session does not
    /// expose it. It is known by construction: the step applies the retention
    /// in place, so whatever this host aimed the retention at is exactly what
    /// the session now holds. If that were wrong the run would diverge
    /// immediately and visibly, which is why no product accessor was added to
    /// make it observable.</para>
    /// </summary>
    private int _storedOffsetX;
    private int _storedOffsetY;

    private int _unreachableLookCommands;
    private int _lookCommands;
    private int _subRetailPixelLookCommands;

    /// <param name="quantizeLookToIntegerMousePixels">
    /// Restrict the driver to stick positions a hand on a mouse can reach
    /// (<see cref="PlayerPlausiblePermille"/>) BEFORE the pointer path is asked
    /// for them.
    ///
    /// <para><b>With it set, this host is lossless</b>, and since 2026-07-30 it
    /// is lossless for a stronger reason than before. The quantisation this
    /// flag applies and the quantisation the client's pointer path applies are
    /// now the SAME law - whole retail mouse pixels - so a driver held to the
    /// lattice asks only for positions the pointer path can deliver exactly.
    /// The loss this class exists to expose is a loss of commands <i>no player
    /// could issue in the first place</i>.</para>
    /// </param>
    internal Level100InteractiveChainHost(
        InteractiveSession session,
        bool quantizeLookToIntegerMousePixels = false)
    {
        _session = session;
        _quantizeLookToIntegerMousePixels = quantizeLookToIntegerMousePixels;
    }

    public WorldSnapshot Snapshot => _session.CurrentSnapshot;

    public IReadOnlyList<int>? ActorRoundDamageIdsForMeasurement => null;

    /// <summary>
    /// How many analogue look commands this run asked for and could not get,
    /// because no whole retail mouse pixel lands on the requested stick
    /// position. Each one is delivered as zero.
    ///
    /// <para>Before 2026-07-30 this also counted commands eaten by an invented
    /// half-pixel dead zone in the client, which is the loss task #141
    /// measured at 44.2 %. That rule is gone; what remains is retail's own
    /// pixel quantum.</para>
    /// </summary>
    internal int UnreachableLookCommands => _unreachableLookCommands;

    /// <summary>How many nonzero analogue look commands were issued at all.</summary>
    internal int LookCommands => _lookCommands;

    /// <summary>
    /// How many analogue look commands are finer than <b>one mouse pixel</b>.
    ///
    /// <para>Retail's <c>CController::DoMappings</c> (0x0042DB40) maps an
    /// INTEGER pixel displacement by <c>sensitivity * 13/3000</c>, and the
    /// image's untouched sensitivity is 7.0, so the smallest look input a hand
    /// on a mouse can produce is <c>91/3000</c> of full deflection - about
    /// <b>30 permille</b>. This client used to be finer than retail there,
    /// because it let Godot's fractional <c>ScreenRelative</c> reach the axis;
    /// since 2026-07-30 the fraction is carried but only whole pixels are read,
    /// so a command below this threshold is one neither surface can
    /// issue.</para>
    /// </summary>
    internal int SubRetailPixelLookCommands => _subRetailPixelLookCommands;

    /// <summary>The permille a one-pixel mouse move produces at the image's
    /// untouched sensitivity.</summary>
    internal const int RetailSinglePixelPermille = 30;

    /// <summary>
    /// The nearest stick position to <paramref name="requested"/> that a hand
    /// on a mouse can actually produce on the released build.
    ///
    /// <para>Retail's <c>CController::DoMappings</c> (0x0042DB40) maps an
    /// INTEGER pixel displacement by <c>sensitivity * 13/3000</c>. At the
    /// image's untouched sensitivity of 7.0 one pixel is <c>91/3000</c> of full
    /// deflection, so the reachable axis is the lattice
    /// <c>{0, ±30, ±61, ±91, ±121, …}</c> permille and NOTHING lies between
    /// zero and thirty. This snaps a requested position to the nearest whole
    /// number of pixels and returns what that many pixels deliver.</para>
    ///
    /// <para><b>Why it exists.</b> Measured over the joined cold-start run,
    /// 2,904 of this driver's 4,937 analogue look commands (58.8 %) are finer
    /// than one mouse pixel; on the pre-2026-07-30 client 2,184 (44.2 %) also
    /// fell inside an invented half-pixel dead zone and arrived as zero. A run
    /// that needs those commands is evidence about the MECHANISM of the level,
    /// not about whether a person can play it. Driving through this function is
    /// the honest playability question - and now that the client quantises to
    /// whole pixels itself, it is also the client's own law rather than a
    /// stricter one bolted on in front of it.</para>
    /// </summary>
    internal static short PlayerPlausiblePermille(short requested)
    {
        int pixels = (int)Math.Round(
            requested * (double)PointerAxisDenominator /
                (PointerAxisNumerator * 1_000.0),
            MidpointRounding.AwayFromZero);
        long scaled = (long)pixels * PointerAxisNumerator * 1_000;
        long permille = scaled >= 0
            ? (scaled + (PointerAxisDenominator / 2)) / PointerAxisDenominator
            : (scaled - (PointerAxisDenominator / 2)) / PointerAxisDenominator;
        return (short)Math.Clamp(permille, -1_000, 1_000);
    }

    /// <summary>
    /// What the client's pointer path actually delivers when a driver asks for
    /// <paramref name="requested"/> permille of stick. Exposed so the loss can
    /// be stated as a property of the surface rather than inferred from one
    /// run's counters.
    /// </summary>
    internal static short DeliverablePermille(short requested)
    {
        int ignored = 0;
        // No Retain here any more: the client reads the axis BEFORE the ease,
        // which is the order 0x0042DB40 and 0x0042DA00 run in.
        int offset = PointerOffsetFor(requested, ref ignored, ref ignored);
        return (short)ToPermille(offset);
    }

    public WorldSnapshot Step(SimInput input)
    {
        _session.ObserveInput(new InteractiveInput(
            input.MoveX,
            input.MoveZ,
            FireHeld: false,
            ToggleModeHeld: false,
            ResetHeld: false,
            input.LookX,
            input.LookY,
            input.HasAction(SimActions.LandingJets)));

        if (input.HasAction(SimActions.Fire))
        {
            _session.QueueFirePulse();
        }

        if (input.HasAction(SimActions.ToggleMode))
        {
            _session.QueueToggleMode();
        }

        if (input.HasAction(SimActions.Reset))
        {
            _session.QueueReset();
        }

        if (input.HasAction(SimActions.ChangeWeapon))
        {
            _session.QueueChangeWeapon();
        }

        // The counters read what the DRIVER ASKED FOR, always, so the
        // sub-pixel census below is a statement about the driver rather than
        // about this host's configuration.
        CountLookCommand(input.LookXAnalogPermille);
        CountLookCommand(input.LookYAnalogPermille);
        short requestedX = _quantizeLookToIntegerMousePixels
            ? PlayerPlausiblePermille(input.LookXAnalogPermille)
            : input.LookXAnalogPermille;
        short requestedY = _quantizeLookToIntegerMousePixels
            ? PlayerPlausiblePermille(input.LookYAnalogPermille)
            : input.LookYAnalogPermille;
        int targetX = PointerOffsetFor(requestedX, ref _lookCommands, ref _unreachableLookCommands);
        int targetY = PointerOffsetFor(requestedY, ref _lookCommands, ref _unreachableLookCommands);
        int deltaX = targetX - _storedOffsetX;
        int deltaY = targetY - _storedOffsetY;
        if (deltaX != 0 || deltaY != 0)
        {
            _session.QueuePointerMotionMilliPixels(deltaX, deltaY);
        }

        FrameAdvanceResult result = _session.AdvanceFrameTicks(FrameElapsedTicks);
        if (result.StepsAdvanced != 1)
        {
            throw new InvalidOperationException(
                $"The client advanced {result.StepsAdvanced} simulation steps on one frame; " +
                "this driver is one frame per tick and every measurement here assumes it.");
        }

        _storedOffsetX = Retain(targetX);
        _storedOffsetY = Retain(targetY);
        return result.CurrentSnapshot;
    }

    private void CountLookCommand(short permille)
    {
        if (permille != 0 && Math.Abs((int)permille) < RetailSinglePixelPermille)
        {
            _subRetailPixelLookCommands++;
        }
    }

    /// <summary>
    /// The whole-pixel cursor offset that has to be standing when the step
    /// reads the axis for the client to deliver <paramref name="permille"/>.
    ///
    /// <para>The client reads BEFORE it eases (retail's own order - see
    /// <c>InteractiveSession.RecenterPointerOffset</c>), so this no longer has
    /// to pre-compensate for the ease: the offset that delivers a stick
    /// position is simply the pixel count that maps onto it.</para>
    ///
    /// <para><b>It SNAPS; it does not refuse.</b> A hand on a mouse cannot
    /// decline to land somewhere, so a request that falls between two lattice
    /// points is delivered as the nearer one - that is quantisation error, not
    /// loss. The only requests delivered as NOTHING are the ones that round to
    /// zero pixels, which at the image's untouched sensitivity means
    /// <c>|permille| &lt;= 15</c>; those are what
    /// <paramref name="unreachable"/> counts.</para>
    ///
    /// <para>The distinction is the whole point of the 2026-07-30 change. The
    /// half-pixel dead zone it replaced <i>did</i> refuse - it returned zero
    /// for permille 1..14 and then delivered 15..1000 exactly - so "unreachable"
    /// used to mean "swallowed by a rule retail does not have". It now means
    /// "smaller than retail's own quantum", and the floor moved by one
    /// permille.</para>
    /// </summary>
    private static int PointerOffsetFor(
        short permille,
        ref int commands,
        ref int unreachable)
    {
        if (permille == 0)
        {
            return 0;
        }

        commands++;
        int magnitude = Math.Abs((int)permille);
        int pixels = (int)Math.Round(
            magnitude * (double)PointerAxisDenominator /
                (PointerAxisNumerator * (double)PointerPixelMilliPixels),
            MidpointRounding.AwayFromZero);
        if (pixels == 0)
        {
            unreachable++;
            return 0;
        }

        int offset = pixels * PointerPixelMilliPixels;
        return permille < 0 ? -offset : offset;
    }

    /// <summary>
    /// <c>InteractiveSession.RecenterPointerOffset</c>, mirrored: the integer
    /// ease with 0x0042DA00's one-pixel anti-stall.
    /// </summary>
    private static int Retain(int value)
    {
        int pixels = value / PointerPixelMilliPixels;
        if (pixels == 0)
        {
            return 0;
        }

        long scaled = (long)pixels * PointerRetentionNumerator;
        int eased = (int)(scaled >= 0
            ? (scaled + (PointerRetentionDenominator / 2)) / PointerRetentionDenominator
            : (scaled - (PointerRetentionDenominator / 2)) / PointerRetentionDenominator);
        if (eased == pixels)
        {
            eased = pixels - Math.Sign(pixels);
        }

        return eased * PointerPixelMilliPixels;
    }

    private static int ToPermille(int offsetMilliPixels)
    {
        long scaled = (long)offsetMilliPixels * PointerAxisNumerator;
        long rounded = scaled >= 0
            ? (scaled + (PointerAxisDenominator / 2)) / PointerAxisDenominator
            : (scaled - (PointerAxisDenominator / 2)) / PointerAxisDenominator;
        return (int)Math.Clamp(rounded, -1_000, 1_000);
    }
}
