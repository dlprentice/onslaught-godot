// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/RadarWarningReceiver.cpp and .h: Init 004d65a0, destructor 004d6600, Update
// 004d66b0, HandleEvent 004d6a10; Equipment.cpp GetPrimaryPosition 0044a850 and BattleEngine.h's alarm flag.
// The scan's arithmetic/order was checked at 004d672b..004d69f0 in the pristine BEA.exe.original.backup:
// SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>SRadarThreat: a monitored round, its bearing/range and the time it was first seen.</summary>
public sealed class RadarThreat
{
    public float Yaw { get; internal set; }
    public float Distance { get; internal set; }
    public short ThingNumber { get; internal init; }
    public ActiveReader<Thing> Round { get; } = new();
    public float Time { get; internal init; }
    public bool Seen { get; internal set; }
}

/// <summary>
/// CRadarWarningReceiver: scans the world's ordered battle-engine-targeting round set. Equipment placement and
/// monitored lifetime use the existing owners. The alarm callback sets the battle engine's incoming-sound flag;
/// it does not play a sound or draw random numbers here. The x87 atan2 approximation retains its existing marker.
/// </summary>
public sealed class RadarWarningReceiver : Equipment
{
    public const int UpdateEvent = 4000;
    private readonly ThingContext _context;
    private readonly SPtrSet<Thing> _rounds;
    private readonly Action _incomingMissileAlarm;
    private bool _deleted;

    public RadarWarningReceiver(ThingContext context, SPtrSet<Thing> rounds, Action incomingMissileAlarm)
    {
        _context = context;
        _rounds = rounds;
        _incomingMissileAlarm = incomingMissileAlarm;
        Threats = new SPtrSet<RadarThreat>(context.Pool);
    }

    public float ScanDistance { get; private set; }
    public float UpdateFrequency { get; private set; }
    public SPtrSet<RadarThreat> Threats { get; }
    public bool NewThreat { get; private set; }
    public float NewThreatTime { get; private set; }

    /// <summary>Init's 4000 is an end-of-frame event at current time plus the configured scan frequency.</summary>
    public void Init(InitEquipment init, float scanDistance, float updateFrequency)
    {
        ArgumentNullException.ThrowIfNull(init.AttachedTo);
        base.Init(init);
        ScanDistance = scanDistance;
        UpdateFrequency = updateFrequency;
        Schedule();
        NewThreat = false;
        NewThreatTime = -1.0f;
    }

    public override void HandleEvent(Event e)
    {
        if (e.EventNum == UpdateEvent) Update();
    }

    /// <summary>
    /// Clear seen, scan, schedule the next scan, notify if the count grew, then forget unseen threats. In particular,
    /// replacing one threat with another still notifies: the previous unseen one is removed only after the count test.
    /// Inactive or dying attached units do not disable this scan; equipment destruction ends its event lifetime.
    /// </summary>
    public void Update()
    {
        ObjectDisposedException.ThrowIf(_deleted, this);
        var clear = new ListIterator<RadarThreat>(Threats);
        for (RadarThreat? threat = clear.First(); threat is not null; threat = clear.Next()) threat.Seen = false;
        int previousCount = Threats.Size;
        var rounds = new ListIterator<Thing>(_rounds);
        for (Thing? item = rounds.First(); item is not null; item = rounds.Next())
        {
            var round = (Round)item;
            // The out-of-line FVector constructor stores each subtraction as a float at 004d672b..004d674c.
            FVector d = round.Pos - GetPrimaryPosition();
            double distance = Distance(d);
            // 004d6774 accepts unordered as well as <=; the damage comparison rejects unordered.
            if (distance > ScanDistance || !ReferenceEquals(round.Target.ToRead, AttachedTo) || !(round.Data.Damage > 0))
                continue;

            RadarThreat? found = null;
            var find = new ListIterator<RadarThreat>(Threats);
            for (RadarThreat? threat = find.First(); threat is not null; threat = find.Next())
            {
                if (ReferenceEquals(threat.Round.ToRead, round)) { found = threat; break; }
            }

            float yaw = WeaponMaths.Azimuth(_context, d);
            if (found is not null)
            {
                found.Seen = true;
                found.Yaw = yaw;
                found.Distance = (float)distance;
            }
            else
            {
                var threat = new RadarThreat
                {
                    Yaw = yaw, Distance = (float)distance, ThingNumber = round.ThingNumber,
                    Seen = true, Time = _context.Events.Time,
                };
                threat.Round.SetReader(round);
                Threats.Add(threat);
                if (AttachedTo!.IsA(ThingType.BattleEngine))
                {
                    // The wrapped facing remains in an x87 register, then yaw-facing is rounded by fsubr.
                    double facing = ((BattleEngine)AttachedTo).CurrentOrientation.Yaw;
                    if (yaw < -WeaponMaths.Pi / 2 && facing > WeaponMaths.Pi / 2)
                        facing = X87.Sub(facing, WeaponMaths.PiM2);
                    else if (yaw > WeaponMaths.Pi / 2 && facing < -WeaponMaths.Pi / 2)
                        facing = X87.Add(facing, WeaponMaths.PiM2);
                    if (Math.Abs(X87.Sub(yaw, facing)) < WeaponMaths.Pi / 4) _incomingMissileAlarm();
                }
            }
        }

        Schedule();
        if (Threats.Size > previousCount)
        {
            NewThreat = true;
            NewThreatTime = _context.Events.Time;
            _context.Events.AddEvent(UnitEvent.IncomingMissileWarning, AttachedTo, EventManager.NextFrame);
        }

        var forget = new ListIterator<RadarThreat>(Threats);
        RadarThreat? old = forget.First();
        while (old is not null)
        {
            if (!old.Seen)
            {
                Threats.Remove(old);
                old.Round.Release();
                old = forget.First();
            }
            else old = forget.Next();
        }
    }

    private static double Distance(FVector d) =>
        X87.Sqrt(X87.Add(X87.Add(X87.Mul(d.Y, d.Y), X87.Mul(d.Z, d.Z)), X87.Mul(d.X, d.X)));

    private void Schedule() => _context.Events.AddEvent(UpdateEvent, this,
        _context.Events.Time + UpdateFrequency, EventPriority.EndOfFrame);

    /// <summary>~CRadarWarningReceiver deletes its threat readers before ~CMonitor clears scheduled targets.</summary>
    public void Delete()
    {
        if (_deleted) return;
        var threats = new ListIterator<RadarThreat>(Threats);
        for (RadarThreat? threat = threats.First(); threat is not null; threat = threats.Next()) threat.Round.Release();
        Threats.RemoveAll();
        Destroy();
        _deleted = true;
    }
}
