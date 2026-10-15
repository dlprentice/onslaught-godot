// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Career;
using Xunit;
using CareerModel = OnslaughtRebuild.Parity.Career.Career;

namespace OnslaughtRebuild.Parity.Tests.Career;

/// <summary>
/// <c>CCareer::UpdateGoodieStates</c> checked against an independent transcription of its rules: one row per line of
/// the C++ (bea-decomp src/Career.cpp, thresholds from src/FEPGoodies.cpp), applied in order by a small oracle to
/// seeded random careers. Order matters because goodies 2 to 7 each require the one before.
/// </summary>
public sealed class CareerGoodieRulesTests
{
    private enum K { Complete, CompleteOrEvo, Grade, GradeOrEvo, Kills, TotalC, TotalS }

    private const int Air = 0, Veh = 1, Emp = 2, Inf = 3, Mech = 4;

    // (goodie, rule, world or kill type, letter or threshold, requires goodie, second kill type, second threshold, grade guard)
    private sealed record Rule(int Goodie, K Kind, int A = 0, int B = 0, int Requires = -1, int A2 = -1, int B2 = 0,
        (K Kind, int World, char Letter)? Also = null);

    private static readonly Rule[] Rules = BuildRules();

    private static Rule[] BuildRules()
    {
        List<Rule> r =
        [
            new(0, K.Complete, 100),
            new(1, K.Grade, 110, 'C'),
            new(2, K.Grade, 200, 'C', Requires: 1),
            new(3, K.GradeOrEvo, 231, 'C', Requires: 2),
            new(4, K.GradeOrEvo, 321, 'C', Requires: 3),
            new(5, K.GradeOrEvo, 321, 'C', Requires: 4),
            new(6, K.GradeOrEvo, 621, 'C', Requires: 5),
            new(7, K.GradeOrEvo, 741, 'C', Requires: 6),
            new(8, K.Complete, 100),
            new(9, K.CompleteOrEvo, 211),
            new(10, K.Complete, 400),
            new(11, K.Complete, 710),
            new(12, K.Complete, 200),
            new(13, K.CompleteOrEvo, 331),
            new(14, K.Complete, 110),
            new(15, K.CompleteOrEvo, 621),
            new(16, K.Grade, 400, 'C'),
            new(17, K.Grade, 300, 'C'),
            new(18, K.Complete, 512),
            new(19, K.CompleteOrEvo, 221),
            new(20, K.CompleteOrEvo, 611),
            new(21, K.CompleteOrEvo, 211),
            new(22, K.Complete, 300),
            new(23, K.Grade, 200, 'C'),
            new(24, K.CompleteOrEvo, 331),
            new(25, K.Complete, 400),
            new(26, K.GradeOrEvo, 221, 'C'),
            new(27, K.CompleteOrEvo, 221),
            new(28, K.Complete, 200),
            new(29, K.Complete, 300),
            new(30, K.CompleteOrEvo, 211),
            new(31, K.CompleteOrEvo, 211),
            new(32, K.Complete, 200),
            new(33, K.Kills, Inf, 40),
            new(34, K.Kills, Inf, 160),
            new(35, K.Kills, Inf, 80),
            new(36, K.Kills, Air, 25),
            new(37, K.Kills, Air, 100),
            new(38, K.Kills, Air, 50),
            new(39, K.Kills, Air, 75),
            new(40, K.Kills, Air, 25, A2: Inf, B2: 80),
            new(41, K.Kills, Air, 50, A2: Inf, B2: 100),
            new(42, K.Kills, Veh, 100),
            new(43, K.Kills, Veh, 400),
            new(44, K.Kills, Veh, 300),
            new(45, K.Kills, Veh, 200),
            new(46, K.Complete, 500),
            new(47, K.Kills, Mech, 20),
            new(48, K.Kills, Mech, 40),
            new(49, K.Kills, Mech, 80),
            new(51, K.Kills, Mech, 40),
            new(53, K.Kills, Emp, 50, A2: Air, B2: 25),
            new(54, K.Kills, Emp, 50),
            new(55, K.Kills, Emp, 25),
            new(56, K.Kills, Emp, 75, A2: Veh, B2: 100),
            new(57, K.Kills, Emp, 25, A2: Air, B2: 25),
            new(58, K.GradeOrEvo, 331, 'A'),
            new(59, K.GradeOrEvo, 431, 'A'),
            new(60, K.CompleteOrEvo, 523),
            new(61, K.GradeOrEvo, 521, 'A'),
            new(62, K.GradeOrEvo, 523, 'A'),
            new(63, K.Kills, Air, 100, Also: (K.GradeOrEvo, 621, 'C')),
            new(64, K.GradeOrEvo, 731, 'A'),
            new(65, K.Grade, 800, 'A'),
            new(66, K.TotalC, 26),
            new(71, K.CompleteOrEvo, 741),
            new(72, K.CompleteOrEvo, 741),
            new(73, K.CompleteOrEvo, 741),
            new(74, K.TotalS, 20),
            new(75, K.TotalS, 40),
            new(76, K.TotalS, 43),
            new(77, K.TotalS, 43),
        ];
        // The concept art, as the C++ lists it: a C grade on every world (78-120), a B grade on every world (121-163),
        // an A grade on every world up to 710 (164-200).
        int[] worlds = [100, 110, 200, 211, 212, 221, 222, 231, 232, 300, 311, 312, 321, 322, 331, 332, 400, 411, 412, 421,
            422, 431, 432, 500, 511, 512, 521, 522, 523, 524, 600, 611, 612, 621, 622, 700, 710, 720, 731, 732, 741, 742, 800];
        int goodie = 78;
        foreach (int world in worlds) r.Add(new(goodie++, K.Grade, world, 'C'));
        foreach (int world in worlds) r.Add(new(goodie++, K.Grade, world, 'B'));
        foreach (int world in worlds.TakeWhile(w => w <= 710)) r.Add(new(goodie++, K.Grade, world, 'A'));
        return [.. r];
    }

    private static readonly int[][] Episodes =
    [
        [0, 1, 8, 14, 33, 36, 41, 42, 43],
        [2, 3, 9, 12, 19, 21, 23, 26, 27, 28, 30, 31, 32, 35, 38, 40],
        [4, 5, 13, 17, 22, 24, 29, 34, 39, 43, 45, 47, 53, 54, 55, 56, 57, 58],
        [10, 16, 25, 44, 48, 52, 59],
        [18, 46, 49, 50, 51, 52, 60, 61, 62],
        [6, 15, 20, 37, 63, 64],
        [7, 11, 64, 66, 67, 68, 69, 70, 71, 74, 75, 76, 77, 78],
        [65, 72, 73],
    ];

    private static readonly int[][] EpisodeWorlds =
        [[], [110], [231, 232], [331, 332], [431, 432], [521, 522, 523, 524], [621, 622], [741, 742]];

    [Fact]
    public void TheTranscriptionCoversEveryRuleOnce()
    {
        Assert.Equal(72 + 123, Rules.Length);
        Assert.Equal(Rules.Length, Rules.Select(rule => rule.Goodie).Distinct().Count());
        Assert.Equal(123, Rules.Count(rule => rule.Goodie >= 78));
    }

    [Fact]
    public void EveryRuleAppliesInTheCppOrderOnRandomCareers()
    {
        float[] rankings = [-1f, -9999f, 0f, 0.1f, 0.25f, 0.3f, 0.5f, 0.6f, 0.75f, 0.9f, 0.99f, 1f, 1.1f];
        for (int seed = 0; seed < 1000; seed++)
        {
            Random random = new(seed);
            CareerModel career = CareerModel.Constructed();
            career.Blank();
            for (int node = 0; node < 43; node++)
            {
                career.Record.SetNodeComplete(node, random.Next(3) == 0 ? 1 : 0);
                career.Record.SetNodeRanking(node, rankings[random.Next(rankings.Length)]);
            }
            for (int type = 0; type < 5; type++) career.Record.SetKilledThings(type, random.Next(0, 500) | (random.Next(256) << 24));
            for (int goodie = 0; goodie < CareerRecord.GoodieCount; goodie++)
                career.Record.SetGoodieState(goodie, (CareerGoodieState)(random.Next(8) switch { 0 => 1, 1 => 2, 2 => 3, _ => 0 }));
            career.Record.PendingExtraGoodies = random.Next(3);
            career.NewGoodieCount = random.Next(5);
            career.FirstGoodie = false;

            (CareerGoodieState[] states, int newCount, bool first) = Oracle(career);
            career.UpdateGoodieStates();

            for (int goodie = 0; goodie < CareerRecord.GoodieCount; goodie++)
                Assert.True(states[goodie] == career.Record.GoodieState(goodie), $"seed {seed}, goodie {goodie}: expected {states[goodie]}, got {career.Record.GoodieState(goodie)}");
            Assert.Equal(newCount, career.NewGoodieCount);
            Assert.Equal(first, career.FirstGoodie);
            Assert.Equal(0, career.Record.PendingExtraGoodies);
        }
    }

    [Fact]
    public void ADependentChainUnlocksInOnePassBecauseOfTheOrder()
    {
        CareerModel career = CareerModel.Constructed();
        career.Blank();
        foreach (int world in new[] { 110, 200, 231, 321, 621, 741 })
        {
            int node = career.GetNodeFromWorldNo(world);
            career.Record.SetNodeComplete(node, 1);
            career.Record.SetNodeRanking(node, 0.5f);
        }
        career.UpdateGoodieStates();
        for (int goodie = 1; goodie <= 7; goodie++) Assert.Equal(CareerGoodieState.New, career.Record.GoodieState(goodie));
    }

    private static (CareerGoodieState[] States, int NewCount, bool First) Oracle(CareerModel career)
    {
        CareerRecord record = career.Record;
        CareerGoodieState[] states = Enumerable.Range(0, CareerRecord.GoodieCount).Select(record.GoodieState).ToArray();
        int Node(int world) => career.GetNodeFromWorldNo(world);
        bool Complete(int world) => record.NodeCompleteFlag(Node(world)) == 1;
        char Grade(int world)
        {
            float ranking = Complete(world) ? record.NodeRanking(Node(world)) : -9999f;
            return GradeOf(ranking);
        }
        bool AtLeast(char grade, char right) => grade == 'S' || (right != 'S' && (sbyte)grade <= (sbyte)right);
        char GradeOrEvo(int world)
        {
            char first = Grade(world), second = Grade(world + 1);
            return AtLeast(first, second) ? first : second;
        }
        int Killed(int type) => record.KilledThings(type) & 0xffffff;
        int CountNew() => states.Count(state => state >= CareerGoodieState.New);
        bool NotDone(int goodie) => states[goodie] <= CareerGoodieState.Instructions;

        int oldCount = CountNew();
        bool firstWasNotDone = NotDone(0);
        foreach (Rule rule in Rules)
        {
            bool fires = rule.Kind switch
            {
                K.Complete => Complete(rule.A),
                K.CompleteOrEvo => Complete(rule.A) || Complete(rule.A + 1),
                K.Grade => AtLeast(Grade(rule.A), (char)rule.B),
                K.GradeOrEvo => AtLeast(GradeOrEvo(rule.A), (char)rule.B),
                K.Kills => Killed(rule.A) >= rule.B && (rule.A2 < 0 || Killed(rule.A2) >= rule.B2),
                K.TotalC => Enumerable.Range(0, 43).Count(n => AtLeast(GradeOf(record.NodeRanking(n)), 'C')) >= rule.A,
                K.TotalS => Enumerable.Range(0, 43).Count(n => GradeOf(record.NodeRanking(n)) == 'S') >= rule.A,
                _ => throw new InvalidOperationException(),
            };
            if (rule.Requires >= 0 && states[rule.Requires] < CareerGoodieState.New) fires = false;
            if (rule.Also is { } also && !AtLeast(GradeOrEvo(also.World), also.Letter)) fires = false;
            if (fires && NotDone(rule.Goodie)) states[rule.Goodie] = CareerGoodieState.New;
        }
        int newCount = CountNew();
        int expectedNew = career.NewGoodieCount + (newCount - oldCount) + record.PendingExtraGoodies;
        bool first = (!NotDone(0) && firstWasNotDone) || (record.PendingExtraGoodies - oldCount == 0 && oldCount != 0);
        for (int episode = 0; episode < Episodes.Length; episode++)
        {
            bool available = episode == 0 || EpisodeWorlds[episode].Any(Complete);
            if (!available) continue;
            foreach (int goodie in Episodes[episode])
            {
                if (NotDone(goodie)) states[goodie] = CareerGoodieState.Instructions;
            }
        }
        return (states, expectedNew, first);
    }

    /// <summary>The grade rule written out for ordinary rankings: S at exactly 1, E at 0 or below, else D, C, B, A by quarters.</summary>
    private static char GradeOf(float ranking)
    {
        if (ranking == 1f) return 'S';
        if (ranking <= 0f) return 'E';
        return (char)('D' - (int)Math.Floor(ranking * 4.0));
    }
}
