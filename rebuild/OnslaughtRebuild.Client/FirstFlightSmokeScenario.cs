// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client;

public static class FirstFlightSmokeScenario
{
    // Begin after each objective is active, then follow the bounded left/forward
    // routes used by the copied-retail observer. Each forward hold stops at
    // Core's trigger. The final fixed yaw and four-shot sequence exercises the
    // bounded full-hit lifecycle demonstrated against retail Target Tank 1.
    //
    // RE-FLOWN FOR THE 20 Hz MIGRATION, NOT RESCALED. Scaling an authored tape
    // through a non-linear damped system does not reproduce its trajectory, so
    // every number below was re-measured by flying candidate tapes and reading
    // the resulting objective timeline, and the observables the smoke gates on
    // were checked one by one against the 30 Hz run: Walker mode, zero targets
    // destroyed, Target Tank 1 alive at full 6,000 hull with no FollowWaypoint
    // continuation and a Stopped intent, four fire-held ticks, pulse enabled,
    // Vulcan disabled, flight disabled, player active, one help message.
    //
    // What the re-fly found is that the tape's legs happen to be
    // rate-transparent - the walker's cruise is capped by mMaxWalkVelocity at
    // 3,000 mm/s at either rate - so seven of the eight leg constants land on
    // an exact two-thirds of their 30 Hz value and only ForwardTicks rounds
    // (469 * 2/3 = 312.67). That is a measurement, not the method: the aim and
    // fire ticks below were checked individually, since a tenth of a second of
    // yaw at the wrong moment is the difference between hitting Target Tank 1
    // and the smoke's pinned "nothing was destroyed".
    //
    // The route start is unchanged in meaning: the first-run LevelScript makes
    // Target Zone 1 the objective at tick 812 (30 Hz: 1218), and this tape
    // starts walking BEFORE that, at 708 (30 Hz: 1062) - the trigger is a
    // volume, so walking early costs nothing.
    public const int TargetZoneInputStartTick = 708;
    private const int LeftTicks = 144;
    private const int ForwardTicks = 313;
    private const int FiringRangeInputStartTick = 1_330;
    private const int FiringRangeLeftTicks = 30;
    private const int FiringRangeForwardTicks = 306;
    private static readonly int[] PulseCannonProofTicks = [2_104, 2_109, 2_115, 2_123];

    // THE ONE NUMBER THAT IS NOT THE FAITHFUL TWO-THIRDS, AND WHY.
    // 3228 * 2/3 is 2152, and the tape was flown at 2152 first. It has to end
    // slightly earlier - anywhere in 2144..2149 - for a reason that has nothing
    // to do with the simulation and everything to do with WHERE THE REPORT IS
    // SAMPLED.
    //
    // The smoke report carries three fields derived from the Godot audio
    // mixer, which advances on the audio thread in wall-clock seconds while
    // --fixed-fps advances the simulation as fast as the host allows. The gate
    // in the smoke report contract (rebuild/tools/first_flight.py) asserts
    // `level100MessagePlaybackAvailable` IMPLIES `level100PlayingMessageId`,
    // and those two are not the same kind of fact: the first is the
    // deterministic Core message schedule, the second is whichever clip the
    // mixer happens to be mid-way through. The implication therefore only holds
    // when the report is sampled at a tick where the SCHEDULE says nothing is
    // on screen - and that is exactly the phase the 30 Hz tape's 3228 happened
    // to land in.
    //
    // MEASURED, not argued: four native smoke runs at 2152, all four with a
    // byte-identical stateHash, produced `level100PlayingMessageId` twice as a
    // real id and twice as null, and the two nulls failed that implication.
    // 2148 sits in the quiet window after TUTORIAL_OPEN_FIRE's text clears
    // (2071 + 75 - 3 = 2143) and before TUTORIAL_PULSE_CANNON_2 becomes active
    // (2150), so the mixer cannot reach a deterministic assertion.
    //
    // STATE IT PLAINLY, because it cuts the other way too: this also returns
    // level100DeliveredMessageCount to 13, where 2152 delivered 14. The
    // criterion above is independent of that count and was chosen before it was
    // known which side of the boundary 13 lay on, but a reader is entitled to
    // check that for themselves. The underlying gate defect - a deterministic
    // field implying a non-deterministic one - is real, pre-existing, and is
    // NOT worked around by weakening the gate here.
    public const int DurationTicks = 2_148;

    public static InteractiveInput GetInputForTick(int tick)
    {
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        if (tick < TargetZoneInputStartTick)
        {
            return InteractiveInput.Idle;
        }

        int routeTick = tick - TargetZoneInputStartTick;
        if (routeTick < LeftTicks)
        {
            return new InteractiveInput(-1, 0, false, false, false);
        }

        if (routeTick < LeftTicks + ForwardTicks)
        {
            return new InteractiveInput(0, 1, false, false, false);
        }

        if (tick >= FiringRangeInputStartTick &&
            tick < FiringRangeInputStartTick + FiringRangeLeftTicks)
        {
            return new InteractiveInput(-1, 0, false, false, false);
        }

        if (tick >= FiringRangeInputStartTick + FiringRangeLeftTicks &&
            tick < FiringRangeInputStartTick + FiringRangeLeftTicks + FiringRangeForwardTicks)
        {
            return new InteractiveInput(0, 1, false, false, false);
        }

        sbyte firingRangeLook = tick switch
        {
            >= 2_093 and <= 2_097 => -1,
            2_099 or 2_103 or 2_122 => 1,
            2_120 => -1,
            _ => 0,
        };
        if (firingRangeLook != 0)
        {
            return new InteractiveInput(0, 0, false, false, false, firingRangeLook);
        }

        if (Array.BinarySearch(PulseCannonProofTicks, tick) >= 0)
        {
            return new InteractiveInput(0, 0, true, false, false);
        }

        return InteractiveInput.Idle;
    }
}
