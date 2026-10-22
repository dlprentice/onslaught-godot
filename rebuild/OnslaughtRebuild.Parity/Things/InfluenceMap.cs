// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/InfluenceMap.h and src/InfluenceMap.cpp (CInfluenceMap: Clear 0048afb0, Load 0048b010,
// Skip 0048b660, Update 0048b7d0, Build 0048b8e0, Decay 0048bf70, AddResidual 0048c000, HandleEvent 0048c120, IsEmpty
// 0048c2d0; CInfluenceNode: ResetInfluence 0048b620, Shutdown 0048c350, Init 0048c390, CalculateInfluence 0048c3b0).
// Not in Stuart's release.

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.World;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// What the influence map reads from a thing on a side's list: CUnit's slots 79 and 80 (GetThreat, GetImportance).
/// Retail calls them through a CUnit cast on squads too, which reaches the squad's own slots 79 and 80 (the members'
/// sums), so units and squads both implement this.
/// </summary>
public interface IThreatSource
{
    float GetThreat();

    float GetImportance();
}

/// <summary>CInfluenceLink: a link to a neighbouring node.</summary>
public sealed class InfluenceLink(InfluenceNode node, int weight)
{
    public InfluenceNode Node { get; } = node;

    public int Weight { get; } = weight;
}

/// <summary>CInfluenceNode (class id 30): a node of the influence map, a complex thing outside the map-who.</summary>
public sealed class InfluenceNode : ComplexThing
{
    public const int ClassIdValue = 30;

    /// <summary>mThreat: Forseti's threat and importance near the node.</summary>
    public readonly float[] Threat = new float[2];

    /// <summary>mThreatB: Muspell's.</summary>
    public readonly float[] ThreatB = new float[2];

    /// <summary>mDistance: hops to the nearest node of each side, 99999 for none.</summary>
    public readonly int[] Distance = new int[2];

    public InfluenceNode(ThingContext context, float x, float y, float radius, float weight)
        : base(context, ClassIdValue)
    {
        Links = new SPtrSet<InfluenceLink>(context.Pool);
        X = x;
        Y = y;
        Radius = radius;
        Weight = weight;
        Side = 0;
        Influence = 0;
        ResetInfluence();
    }

    public SPtrSet<InfluenceLink> Links { get; }

    public float X;
    public float Y;
    public float Radius;

    /// <summary>mWeight: the node's fourth stored word (version 1); its purpose is unknown.</summary>
    public float Weight;

    /// <summary>mInfluence: eased towards <see cref="Target"/>.</summary>
    public float Influence;

    /// <summary>mTarget: -1 (Muspell) .. 1 (Forseti).</summary>
    public float Target;

    /// <summary>mSide: 0, or 1 or 2 when a side holds the node.</summary>
    public int Side;

    /// <summary>GetRadius (CThing slot 16).</summary>
    public override float GetRadius() => Radius;

    public void AddLink(InfluenceNode node, int weight) => Links.Add(new InfluenceLink(node, weight));

    /// <summary>CInfluenceNode::Init: no collision, out of the map-who, then CComplexThing::Init.</summary>
    public override void Init(InitThing init)
    {
        init.InitCst.NotSeekCollisionWith = ThingType.Everything;
        Flags &= ~ThingFlags.InMapWho;
        base.Init(init);
    }

    /// <summary>CInfluenceNode::Shutdown: the links go, then CComplexThing's.</summary>
    public override void Shutdown()
    {
        InfluenceLink? l;
        while ((l = Links.First()) is not null)
        {
            Links.Remove(l);
        }

        base.Shutdown();
    }

    /// <summary>ResetInfluence: the threat sums, the target and the distances (not the influence or the side).</summary>
    public void ResetInfluence()
    {
        Threat[0] = 0;
        Threat[1] = 0;
        ThreatB[0] = 0;
        ThreatB[1] = 0;
        Target = 0;
        Distance[0] = 99999;
        Distance[1] = 99999;
    }

    /// <summary>
    /// CalculateInfluence: the target is the balance of the two sides' threat here, or, with none, a lean towards the
    /// side with more threat on the linked nodes; easing moves the influence 0.05 towards it per step.
    /// </summary>
    public void CalculateInfluence(bool ease)
    {
        float total = Threat[0] + ThreatB[0];
        if (total > 0.0f)
        {
            Target = (Threat[0] - ThreatB[0]) / total;
            if (Influence < 0.0f)
            {
                Side = 1;
            }
            else if (Influence > 0.0f)
            {
                Side = 2;
            }
            else
            {
                Side = 0;
            }
        }
        else
        {
            Target = 0;
            float a = 0.0f;
            float b = 0.0f;
            for (InfluenceLink? l = Links.First(); l is not null; l = Links.Next())
            {
                a += l.Node.Threat[0];
                b += l.Node.ThreatB[0];
            }

            Side = !(a < b) ? 2 : 1;
        }

        if (Target == 0.0f)
        {
            switch (Side)
            {
                case 1:
                    Target = -1.0f;
                    break;
                case 2:
                    Target = 1.0f;
                    break;
            }
        }

        if (ease)
        {
            if (Influence + 0.05f < Target)
            {
                Influence += 0.05f;
            }
            else if (Influence - 0.05f > Target)
            {
                Influence -= 0.05f;
            }
            else
            {
                Influence = Target;
            }
        }
        else
        {
            Influence = Target;
        }
    }
}

/// <summary>CInfluenceResidual: threat left behind at a place, fading.</summary>
public sealed class InfluenceResidual(InfluenceNode? node, float threat, int allegiance)
{
    /// <summary>mNode: the nearest node when the residual was made.</summary>
    public InfluenceNode? Node { get; } = node;

    public float Threat = threat;

    public int Allegiance { get; } = allegiance;
}

/// <summary>
/// CInfluenceMap (INFLUENCEMAP): the graph of nodes a world file carries, on which each side's threat is summed. Three
/// events drive it: 1000 rebuilds the sums (every one to two seconds, a draw each time), 1001 eases the nodes (every
/// quarter second) and 1002 fades the residuals (every half second).
/// </summary>
public sealed class InfluenceMap : Monitor
{
    public const int BuildEvent = 1000;
    public const int UpdateEvent = 1001;
    public const int DecayEvent = 1002;

    private readonly ThingContext _context;

    public InfluenceMap(ThingContext context)
    {
        _context = context;
        Nodes = new SPtrSet<InfluenceNode>(context.Pool);
        Residuals = new SPtrSet<InfluenceResidual>(context.Pool);
    }

    public SPtrSet<InfluenceNode> Nodes { get; }

    public SPtrSet<InfluenceResidual> Residuals { get; }

    /// <summary>Clear: every node shuts down; the residuals go.</summary>
    public void Clear()
    {
        InfluenceNode? n;
        while ((n = Nodes.First()) is not null)
        {
            Nodes.Remove(n);
            n.Shutdown();
        }

        InfluenceResidual? r;
        while ((r = Residuals.First()) is not null)
        {
            Residuals.Remove(r);
        }
    }

    /// <summary>
    /// Load: the nodes (each a thing, Init'd at its X and Y with Z 0) and both directions of each link, then Build(FALSE)
    /// (a draw for its event) and Decay. A version other than 0 or 1 has no nodes but still builds and decays.
    /// </summary>
    public void Load(InfluenceMapRecord record)
    {
        Clear();
        if (record.Version is 0 or 1)
        {
            foreach ((float x, float y, float radius, int weightBits) in record.Nodes)
            {
                // Version 1 reads the node weight as a float (the record keeps its bits); version 0 passes 0.
                float weight = record.Version == 1 ? BitConverter.Int32BitsToSingle(weightBits) : 0;
                var n = new InfluenceNode(_context, x, y, radius, weight);
                var init = new InitThing { Pos = new FVector(x, y, 0) };
                n.Init(init);
                Nodes.Append(n);
            }

            foreach ((int a, int b, int weight) in record.Links)
            {
                InfluenceNode na = GetNode(a) ?? throw new InvalidDataException($"influence link to missing node {a}");
                InfluenceNode nb = GetNode(b) ?? throw new InvalidDataException($"influence link to missing node {b}");
                na.AddLink(nb, weight);
                nb.AddLink(na, weight);
            }
        }

        Build(false);
        Decay();
    }

    /// <summary>The node at a position in the list, or null.</summary>
    public InfluenceNode? GetNode(int index)
    {
        int i = 0;
        for (InfluenceNode? n = Nodes.First(); n is not null; n = Nodes.Next())
        {
            if (i == index)
            {
                return n;
            }

            i++;
        }

        return null;
    }

    /// <summary>
    /// GetNearestNode: by |dx| + |dy| less the node's radius; the first node that contains the point wins at once.
    /// </summary>
    public InfluenceNode? GetNearestNode(float x, float y)
    {
        float bestDist = 999999.0f;
        InfluenceNode? best = null;
        for (InfluenceNode? n = Nodes.First(); n is not null; n = Nodes.Next())
        {
            float dx = MathF.Abs(x - n.X);
            float dy = MathF.Abs(y - n.Y);
            float d = dx + dy;
            d -= n.GetRadius();
            if (d < 0.0f)
            {
                return n;
            }

            if (d < bestDist)
            {
                bestDist = d;
                best = n;
            }
        }

        return best;
    }

    /// <summary>Update (event 1001): every node eases, then each side's distance spreads twenty times.</summary>
    public void Update()
    {
        InfluenceNode? n;
        for (n = Nodes.First(); n is not null; n = Nodes.Next())
        {
            n.CalculateInfluence(true);
        }

        for (int i = 20; i != 0; i--)
        {
            for (n = Nodes.First(); n is not null; n = Nodes.Next())
            {
                SpreadDistances(n);
            }
        }

        _context.Events.AddEvent(UpdateEvent, this, _context.Events.Time + 0.25f);
    }

    private static void SpreadDistances(InfluenceNode n)
    {
        for (InfluenceLink? l = n.Links.First(); l is not null; l = n.Links.Next())
        {
            if (n.Distance[1] > l.Node.Distance[1] + 1)
            {
                n.Distance[1] = l.Node.Distance[1] + 1;
            }

            if (n.Distance[0] > l.Node.Distance[0] + 1)
            {
                n.Distance[0] = l.Node.Distance[0] + 1;
            }
        }
    }

    /// <summary>
    /// Build (event 1000): each side's threat and importance on the node nearest each of its ground units and squads
    /// (Forseti's list is CWorld +0x20, Muspell's +0x30), and the residuals; held nodes are marked, ten passes spread
    /// threat into empty nodes from their links, twenty spread the distances, every node sets its influence, and the
    /// next build comes FloatRandom() + 1 seconds later.
    /// </summary>
    public void Build(bool ease)
    {
        InfluenceNode? n;
        for (n = Nodes.First(); n is not null; n = Nodes.Next())
        {
            n.ResetInfluence();
        }

        SumSide(_context.World.Set20, forseti: true);
        SumSide(_context.World.Set30, forseti: false);

        var rit = new ListIterator<InfluenceResidual>(Residuals);
        for (InfluenceResidual? r = rit.First(); r is not null; r = rit.Next())
        {
            if (r.Node is null)
            {
                continue;
            }

            if (r.Allegiance == 0)
            {
                r.Node.Threat[0] += r.Threat;
                r.Node.Threat[1] += r.Threat;
            }
            else if (r.Allegiance == 1)
            {
                r.Node.ThreatB[0] += r.Threat;
                r.Node.ThreatB[1] += r.Threat;
            }
        }

        for (n = Nodes.First(); n is not null; n = Nodes.Next())
        {
            if (n.Threat[0] != 0)
            {
                n.Distance[1] = 0;
            }

            if (n.ThreatB[0] != 0)
            {
                n.Distance[0] = 0;
            }
        }

        for (int i = 10; i != 0; i--)
        {
            for (n = Nodes.First(); n is not null; n = Nodes.Next())
            {
                if (n.Threat[0] + n.ThreatB[0] == 0)
                {
                    float sum = 0;
                    float sumB = 0;
                    for (InfluenceLink? l = n.Links.First(); l is not null; l = n.Links.Next())
                    {
                        sum += l.Node.Threat[0];
                        sumB += l.Node.ThreatB[0];
                    }

                    n.Threat[0] = sum * 0.1f;
                    n.ThreatB[0] = sumB * 0.1f;
                }
            }
        }

        for (int i = 20; i != 0; i--)
        {
            for (n = Nodes.First(); n is not null; n = Nodes.Next())
            {
                SpreadDistances(n);
            }
        }

        for (n = Nodes.First(); n is not null; n = Nodes.Next())
        {
            n.CalculateInfluence(ease);
        }

        _context.Events.AddEvent(BuildEvent, this, _context.Random.FloatRandom() + _context.Events.Time + 1.0f);
    }

    private void SumSide(SPtrSet<Thing> things, bool forseti)
    {
        var it = new ListIterator<Thing>(things);
        for (Thing? thing = it.First(); thing is not null; thing = it.Next())
        {
            if (thing.IsDying)
            {
                continue;
            }

            bool counted;
            if ((thing.ThingTypeBits & ThingType.Unit) != 0)
            {
                counted = (thing.ThingTypeBits & (ThingType.Component | ThingType.Naval | ThingType.AirUnit)) == 0;
            }
            else
            {
                counted = (thing.ThingTypeBits & ThingType.Unknown20000000) != 0;
            }

            if (!counted)
            {
                continue;
            }

            InfluenceNode? n = GetNearestNode(thing.Pos.X, thing.Pos.Y);
            if (n is not null)
            {
                var source = thing as IThreatSource
                    ?? throw new InvalidOperationException($"{thing} is on a side's list but has no threat (CUnit slots 79/80)");
                float[] sums = forseti ? n.Threat : n.ThreatB;
                sums[0] += source.GetThreat();
                sums[1] += source.GetImportance();
            }
        }
    }

    /// <summary>Decay (event 1002): each residual fades by 0.2; the first one gone is removed and ends the pass.</summary>
    public void Decay()
    {
        var it = new ListIterator<InfluenceResidual>(Residuals);
        for (InfluenceResidual? r = it.First(); r is not null; r = it.Next())
        {
            r.Threat -= 0.2f;
            if (r.Threat <= 0.0f)
            {
                Residuals.Remove(r);
                break;
            }
        }

        _context.Events.AddEvent(DecayEvent, this, _context.Events.Time + 0.5f);
    }

    /// <summary>AddResidual: threat at a place, on the nearest node now, at the head of the list.</summary>
    public void AddResidual(float x, float y, float threat, int allegiance) =>
        Residuals.Add(new InfluenceResidual(GetNearestNode(x, y), threat, allegiance));

    public bool IsEmpty() => Nodes.Size <= 0;

    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case BuildEvent:
                Build(true);
                break;
            case UpdateEvent:
                Update();
                break;
            case DecayEvent:
                Decay();
                break;
        }
    }
}
