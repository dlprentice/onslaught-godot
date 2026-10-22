// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/Career.cpp (CCareer) and src/FEPGoodies.cpp (the goodie thresholds it reads).

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Career;

/// <summary>
/// The game's career: <c>CCareer</c> and the two file-scope counters beside it in Career.cpp
/// (<c>new_goodie_count</c>, <c>first_goodie</c>). One instance stands for the game's global <c>CAREER</c>; its
/// state is a <see cref="CareerRecord"/> so loads, blanks and saves keep every byte as the game does. The parity
/// core calls <see cref="Update"/> at the end of a level; the front end uses the same object for new careers,
/// loading, the debriefing counters and saving.
/// </summary>
public sealed class Career
{
    private const int NumNodes = 43;
    private const int BaseThingsExistsSize = 288;
    private const int SlotRange = CareerRecord.SlotWordCount * 8;

    public Career(CareerRecord record) => Record = record;

    /// <summary>The game's global career as static construction leaves it, before any load.</summary>
    public static Career Constructed() => new(CareerRecord.Constructed());

    public CareerRecord Record { get; }

    /// <summary><c>new_goodie_count</c>: goodies earned since the debriefing last read it.</summary>
    public int NewGoodieCount { get; set; }

    /// <summary><c>first_goodie</c>.</summary>
    public bool FirstGoodie { get; set; }

    // --- Blank, lookups ---------------------------------------------------------------------------------------

    /// <summary>
    /// <c>CCareer::Blank</c> (0x0041b7c0): the 43 campaign nodes and their 86 links are reset from the level table;
    /// nodes and links beyond them, the volumes, the controller settings and each node's island flag keep their bytes.
    /// </summary>
    public void Blank()
    {
        int link = 0;
        for (int node = 0; node < NumNodes; node++)
        {
            Record.BlankNode(node);
            Record.SetNodeWorldNumber(node, CareerTables.LevelStructure[node, 0]);
            Record.SetLinkToNode(link, CareerTables.LevelStructure[node, 1]);
            Record.SetLinkToNode(link + 1, CareerTables.LevelStructure[node, 2]);
            Record.SetLinkType(link, CareerLinkType.NotComplete);
            Record.SetLinkType(link + 1, CareerLinkType.NotComplete);
            Record.SetNodeLowerLink(node, link);
            Record.SetNodeHigherLink(node, link + 1);
            link += 2;
        }
        for (int goodie = 0; goodie < CareerRecord.GoodieCount; goodie++) Record.SetGoodieState(goodie, CareerGoodieState.Unknown);
        for (int type = 0; type < CareerRecord.KillTypeCount; type++) Record.SetKilledThings(type, 0);
        for (int word = 0; word < CareerRecord.SlotWordCount; word++) Record.SetSlotWord(word, 0);
        NewGoodieCount = 0;
        FirstGoodie = false;
        Record.CareerInProgress = 0;
        Record.SetIsGod(0, 0);
        Record.SetIsGod(1, 0);
        Record.PendingExtraGoodies = 0;
        UpdateGoodieStates();
    }

    /// <summary><c>CCareer::GetNodeFromWorldNo</c> (0x0041b8f0): the node index, or -1.</summary>
    public int GetNodeFromWorldNo(int worldNumber)
    {
        for (int node = 0; node < NumNodes; node++)
        {
            if (Record.NodeWorldNumber(node) == worldNumber) return node;
        }
        return -1;
    }

    /// <summary><c>CCareerNode::DoesBaseThingExist</c>.</summary>
    public bool NodeBaseThingExists(int node, int offset)
    {
        int word = offset >> 5;
        int bit = offset & 31;
        int mask = 1;
        if (bit > 0) mask <<= bit;
        return (Record.NodeBaseThingsWord(node, word) & mask) != 0;
    }

    /// <summary><c>CCareerNode::SetBaseThingExistTo</c> (0x0041b770): only a value of exactly 1 sets the bit.</summary>
    public void SetNodeBaseThingExists(int node, int offset, int value)
    {
        int word = offset >> 5;
        int bit = offset & 31;
        int mask = 1;
        if (bit > 0) mask <<= bit;
        int current = Record.NodeBaseThingsWord(node, word);
        Record.SetNodeBaseThingsWord(node, word, value == 1 ? current | mask : current & ~mask);
    }

    /// <summary><c>CCareer::DoesBaseThingExist</c> (0x0041bb20).</summary>
    public bool DoesBaseThingExist(int worldNumber, int offset, bool multiplayer)
    {
        if (multiplayer) return true;
        int node = GetNodeFromWorldNo(worldNumber);
        return node < 0 || NodeBaseThingExists(node, offset);
    }

    /// <summary><c>CCareer::IsWorldLater</c> (0x0041bbb0).</summary>
    public bool IsWorldLater(int currentWorld, int diesOnWorld)
    {
        int current = GetNodeFromWorldNo(currentWorld);
        int diesOn = GetNodeFromWorldNo(diesOnWorld);
        return current >= 0 && diesOn >= 0 && current != diesOn && Later(diesOn, current);
    }

    /// <summary><c>CCareer::Later</c> (0x0041bc60).</summary>
    public bool Later(int diesOnNode, int currentNode)
    {
        if (currentNode < 0 || diesOnNode < 0) return false;
        if (currentNode == diesOnNode) return true;
        int lower = Record.LinkToNode(Record.NodeLowerLinkIndex(diesOnNode));
        if (lower != -1 && Later(lower, currentNode)) return true;
        int higher = Record.LinkToNode(Record.NodeHigherLinkIndex(diesOnNode));
        return higher != -1 && Later(higher, currentNode);
    }

    // --- End of level -----------------------------------------------------------------------------------------

    /// <summary><c>CCareer::Update</c> (0x0041bd00): the career after a level ends.</summary>
    public void Update(EndLevelData end)
    {
        if (end.FinalState != EndLevelState.Won)
        {
            UpdateGoodieStates();
            return;
        }
        for (int word = 0; word < CareerRecord.SlotWordCount; word++) Record.SetSlotWord(word, end.Slots[word]);
        UpdateThingsKilled(end);
        int node = GetNodeFromWorldNo(end.WorldFinished);
        if (node < 0) return;
        // x87: fcomp of two stored floats, exact in any precision; the C# float compare is the same.
        if (end.Ranking > Record.NodeRanking(node)) Record.SetNodeRanking(node, end.Ranking);
        Record.SetNodeComplete(node, 1);
        Record.CareerInProgress = 1;
        ReCalcLinks(end);
        UpdateGoodieStates();
    }

    /// <summary><c>CCareer::ReCalcLinks</c> (0x0041bdf0).</summary>
    private void ReCalcLinks(EndLevelData end)
    {
        int finished = GetNodeFromWorldNo(end.WorldFinished);
        int higherLink = Record.NodeHigherLinkIndex(finished);
        foreach (int link in new[] { Record.NodeLowerLinkIndex(finished), higherLink })
        {
            bool isHigher = link == higherLink;
            for (int node = 0; node < NumNodes; node++)
            {
                if (Record.NodeWorldNumber(node) != end.WorldFinished) continue;
                if (CareerTables.LevelStructure[node, 3] != -1)
                    UpdateBaseWorldExistsStuffForNode(GetNodeFromWorldNo(CareerTables.LevelStructure[node, 3]), end);
                if (end.IsAllSecondaryObjectivesComplete() && CareerTables.LevelStructure[node, 4] != -1)
                    UpdateBaseWorldExistsStuffForNode(GetNodeFromWorldNo(CareerTables.LevelStructure[node, 4]), end);
            }
            if (Record.LinkType(link) == CareerLinkType.Complete) continue;
            bool complete;
            if (end.WorldFinished == 500)
            {
                complete = false;
                if (GetSlot(CareerTables.Slot500Rocket) && link == higherLink) complete = true;
                if (GetSlot(CareerTables.Slot500Sub) && link != higherLink) complete = true;
            }
            else if (isHigher)
            {
                complete = end.IsAllSecondaryObjectivesComplete();
            }
            else
            {
                complete = true;
            }
            if (!complete) continue;
            Record.SetLinkType(link, CareerLinkType.Complete);
            int toNode = Record.LinkToNode(link);
            if (toNode < 0) continue;
            foreach (int parent in ParentLinks(toNode))
            {
                if (parent != link && Record.LinkType(parent) == CareerLinkType.Complete)
                    Record.SetLinkType(parent, CareerLinkType.CompleteBroken);
            }
        }
    }

    /// <summary><c>CCareerNode::GetParentLinks</c> (0x0041b9f0): every node's lower then higher link that leads to the node.</summary>
    public IEnumerable<int> ParentLinks(int toNode)
    {
        for (int node = 0; node < NumNodes; node++)
        {
            foreach (int link in new[] { Record.NodeLowerLinkIndex(node), Record.NodeHigherLinkIndex(node) })
            {
                if (Record.LinkToNode(link) == toNode) yield return link;
            }
        }
    }

    private void UpdateBaseWorldExistsStuffForNode(int node, EndLevelData end)
    {
        for (int thing = 0; thing < BaseThingsExistsSize; thing++) SetNodeBaseThingExists(node, thing, end.BaseThingsLeft[thing]);
    }

    /// <summary><c>CCareer::GetNumKilled</c> (0x0041c160): the low 24 bits; the top byte of two counters holds the screen position.</summary>
    public int GetNumKilled(KillType type) => Record.KilledThings((int)type) & 0xffffff;

    /// <summary><c>CCareer::UpdateThingsKilled</c> (0x0041c180): nothing is scored on the training level.</summary>
    private void UpdateThingsKilled(EndLevelData end)
    {
        if (end.WorldFinished == 100) return;
        for (int type = 0; type < CareerRecord.KillTypeCount; type++)
            Record.SetKilledThings(type, unchecked(Record.KilledThings(type) + end.ThingsKilled[type]));
    }

    // --- Grades and goodies -----------------------------------------------------------------------------------

    /// <summary>
    /// <c>CCareer::GetGradeFromRanking</c> (0x00421470), as the byte of the one-character string it builds: 'S' for
    /// exactly 1, 'E' for 0 or less, else 'D' minus floor(f*4). A zero byte ends the string, so the grade is then 0;
    /// NaN, infinity and rankings too large for the conversion give 'D' (the conversion's integer indefinite has a zero
    /// low half).
    /// </summary>
    public static sbyte GradeFromRanking(float ranking)
    {
        // x87: compares of a float with a float constant are exact, so plain C# compares match.
        if (ranking == 1f) return (sbyte)'S';
        if (ranking <= 0f) return (sbyte)'E';
        // x87: f * 4.f rounded to 24 bits in the register's wide exponent range (so a huge ranking stays finite),
        // floorf (exact on the register value), then the /QIfist conversion.
        int i = X87.Fist(Math.Floor(X87.Mul(ranking, 4.0)));
        return unchecked((sbyte)('D' - i));
    }

    /// <summary><c>CGrade::operator&gt;=</c>: S beats everything; otherwise a lower letter is a better grade (signed char compare).</summary>
    public static bool GradeAtLeast(sbyte grade, sbyte right)
    {
        if (grade == 'S') return true;
        if (right == 'S') return false;
        return grade <= right;
    }

    private bool CompleteLevel(int worldNumber) => Record.NodeCompleteFlag(RequiredNode(worldNumber)) == 1;

    private bool CompleteLevelOrEvo(int worldNumber) => CompleteLevel(worldNumber) || CompleteLevel(worldNumber + 1);

    /// <summary><c>GRADE</c> (0x0041c330): the grade of a completed world, or of -9999 if not completed.</summary>
    private sbyte Grade(int worldNumber)
    {
        float ranking = -9999f;
        if (CompleteLevel(worldNumber)) ranking = Record.NodeRanking(GetNodeFromWorldNo(worldNumber));
        return GradeFromRanking(ranking);
    }

    private sbyte GradeOrEvo(int worldNumber)
    {
        sbyte first = Grade(worldNumber);
        sbyte second = Grade(worldNumber + 1);
        return GradeAtLeast(first, second) ? first : second;
    }

    private static bool AtLeast(sbyte grade, char letter) => GradeAtLeast(grade, (sbyte)letter);

    private bool TotalGrades(int goodie, Func<sbyte, bool> counts)
    {
        int found = 0;
        for (int node = 0; node < NumNodes; node++)
        {
            if (counts(GradeFromRanking(Record.NodeRanking(node)))) found++;
        }
        return found >= CareerTables.GoodieNumber(goodie);
    }

    private bool TotalCGrades(int goodie) => TotalGrades(goodie, grade => GradeAtLeast(grade, (sbyte)'C'));
    private bool TotalSGrades(int goodie) => TotalGrades(goodie, grade => grade == 'S');

    private bool Dead(KillType type, int goodie, bool second) =>
        GetNumKilled(type) >= (second ? CareerTables.GoodieNumber2(goodie) : CareerTables.GoodieNumber(goodie));

    private bool GoodieNotDone(int goodie) => Record.GoodieState(goodie) <= CareerGoodieState.Instructions;
    private bool GoodieUnlocked(int goodie) => Record.GoodieState(goodie) >= CareerGoodieState.New;

    private void SetGoodieNew(int goodie)
    {
        if (GoodieNotDone(goodie)) Record.SetGoodieState(goodie, CareerGoodieState.New);
    }

    private void SetGoodieInstruction(int goodie)
    {
        if (GoodieNotDone(goodie)) Record.SetGoodieState(goodie, CareerGoodieState.Instructions);
    }

    /// <summary><c>CCareer::CountGoodies</c> (0x0041c450).</summary>
    public int CountGoodies()
    {
        int total = 0;
        for (int goodie = 0; goodie < CareerRecord.GoodieCount; goodie++)
        {
            if (Record.GoodieState(goodie) >= CareerGoodieState.New) total++;
        }
        return total;
    }

    /// <summary><c>CCareer::UpdateGoodieStates</c> (0x0041c470), every rule in the game's order.</summary>
    public void UpdateGoodieStates()
    {
        int oldCount = CountGoodies();
        bool firstWasNotDone = Record.GoodieState(0) <= CareerGoodieState.Instructions;

        if (CompleteLevel(100)) SetGoodieNew(0);
        if (AtLeast(Grade(110), 'C')) SetGoodieNew(1);
        if (GoodieUnlocked(1) && AtLeast(Grade(200), 'C')) SetGoodieNew(2);
        if (GoodieUnlocked(2) && AtLeast(GradeOrEvo(231), 'C')) SetGoodieNew(3);
        if (GoodieUnlocked(3) && AtLeast(GradeOrEvo(321), 'C')) SetGoodieNew(4);
        if (GoodieUnlocked(4) && AtLeast(GradeOrEvo(321), 'C')) SetGoodieNew(5);
        if (GoodieUnlocked(5) && AtLeast(GradeOrEvo(621), 'C')) SetGoodieNew(6);
        if (GoodieUnlocked(6) && AtLeast(GradeOrEvo(741), 'C')) SetGoodieNew(7);
        if (CompleteLevel(100)) SetGoodieNew(8);
        if (CompleteLevelOrEvo(211)) SetGoodieNew(9);
        if (CompleteLevel(400)) SetGoodieNew(10);
        if (CompleteLevel(710)) SetGoodieNew(11);
        if (CompleteLevel(200)) SetGoodieNew(12);
        if (CompleteLevelOrEvo(331)) SetGoodieNew(13);
        if (CompleteLevel(110)) SetGoodieNew(14);
        if (CompleteLevelOrEvo(621)) SetGoodieNew(15);
        if (AtLeast(Grade(400), 'C')) SetGoodieNew(16);
        if (AtLeast(Grade(300), 'C')) SetGoodieNew(17);
        if (CompleteLevel(512)) SetGoodieNew(18);
        if (CompleteLevelOrEvo(221)) SetGoodieNew(19);
        if (CompleteLevelOrEvo(611)) SetGoodieNew(20);
        if (CompleteLevelOrEvo(211)) SetGoodieNew(21);
        if (CompleteLevel(300)) SetGoodieNew(22);
        if (AtLeast(Grade(200), 'C')) SetGoodieNew(23);
        if (CompleteLevelOrEvo(331)) SetGoodieNew(24);
        if (CompleteLevel(400)) SetGoodieNew(25);
        if (AtLeast(GradeOrEvo(221), 'C')) SetGoodieNew(26);
        if (CompleteLevelOrEvo(221)) SetGoodieNew(27);
        if (CompleteLevel(200)) SetGoodieNew(28);
        if (CompleteLevel(300)) SetGoodieNew(29);
        if (CompleteLevelOrEvo(211)) SetGoodieNew(30);
        if (CompleteLevelOrEvo(211)) SetGoodieNew(31);
        if (CompleteLevel(200)) SetGoodieNew(32);
        if (Dead(KillType.Infantry, 33, false)) SetGoodieNew(33);
        if (Dead(KillType.Infantry, 34, false)) SetGoodieNew(34);
        if (Dead(KillType.Infantry, 35, false)) SetGoodieNew(35);
        if (Dead(KillType.Aircraft, 36, false)) SetGoodieNew(36);
        if (Dead(KillType.Aircraft, 37, false)) SetGoodieNew(37);
        if (Dead(KillType.Aircraft, 38, false)) SetGoodieNew(38);
        if (Dead(KillType.Aircraft, 39, false)) SetGoodieNew(39);
        if (Dead(KillType.Aircraft, 40, false) && Dead(KillType.Infantry, 40, true)) SetGoodieNew(40);
        if (Dead(KillType.Aircraft, 41, false) && Dead(KillType.Infantry, 41, true)) SetGoodieNew(41);
        if (Dead(KillType.Vehicles, 42, false)) SetGoodieNew(42);
        if (Dead(KillType.Vehicles, 43, false)) SetGoodieNew(43);
        if (Dead(KillType.Vehicles, 44, false)) SetGoodieNew(44);
        if (Dead(KillType.Vehicles, 45, false)) SetGoodieNew(45);
        if (CompleteLevel(500)) SetGoodieNew(46);
        if (Dead(KillType.Mechs, 47, false)) SetGoodieNew(47);
        if (Dead(KillType.Mechs, 48, false)) SetGoodieNew(48);
        if (Dead(KillType.Mechs, 49, false)) SetGoodieNew(49);
        // Goodie 50 is set by mission script.
        if (Dead(KillType.Mechs, 51, false)) SetGoodieNew(51);
        // Goodie 52 is set by mission script.
        if (Dead(KillType.Emplacements, 53, false) && Dead(KillType.Aircraft, 53, true)) SetGoodieNew(53);
        if (Dead(KillType.Emplacements, 54, false)) SetGoodieNew(54);
        if (Dead(KillType.Emplacements, 55, false)) SetGoodieNew(55);
        if (Dead(KillType.Emplacements, 56, false) && Dead(KillType.Vehicles, 56, true)) SetGoodieNew(56);
        if (Dead(KillType.Emplacements, 57, false) && Dead(KillType.Aircraft, 57, true)) SetGoodieNew(57);
        if (AtLeast(GradeOrEvo(331), 'A')) SetGoodieNew(58);
        if (AtLeast(GradeOrEvo(431), 'A')) SetGoodieNew(59);
        if (CompleteLevelOrEvo(523)) SetGoodieNew(60);
        if (AtLeast(GradeOrEvo(521), 'A')) SetGoodieNew(61);
        if (AtLeast(GradeOrEvo(523), 'A')) SetGoodieNew(62);
        if (Dead(KillType.Aircraft, 63, false) && AtLeast(GradeOrEvo(621), 'C')) SetGoodieNew(63);
        if (AtLeast(GradeOrEvo(731), 'A')) SetGoodieNew(64);
        if (AtLeast(Grade(800), 'A')) SetGoodieNew(65);
        if (TotalCGrades(66)) SetGoodieNew(66);
        // Goodies 67 to 70, the races, are set by mission script.
        if (CompleteLevelOrEvo(741)) SetGoodieNew(71);
        if (CompleteLevelOrEvo(741)) SetGoodieNew(72);
        if (CompleteLevelOrEvo(741)) SetGoodieNew(73);
        if (TotalSGrades(74)) SetGoodieNew(74);
        if (TotalSGrades(75)) SetGoodieNew(75);
        if (TotalSGrades(76)) SetGoodieNew(76);
        if (TotalSGrades(77)) SetGoodieNew(77);

        // Concept art: a C, a B and an A grade on each world in node order (the A run stops at world 710, goodie 200).
        int goodie = 78;
        foreach (char letter in new[] { 'C', 'B', 'A' })
        {
            foreach (int world in CareerTables.ConceptArtWorlds)
            {
                if (goodie > 200) break;
                if (AtLeast(Grade(world), letter)) SetGoodieNew(goodie);
                goodie++;
            }
        }

        int newCount = CountGoodies();
        NewGoodieCount = unchecked(NewGoodieCount + (newCount - oldCount) + Record.PendingExtraGoodies);
        if ((!GoodieNotDone(0) && firstWasNotDone) || (Record.PendingExtraGoodies - oldCount == 0 && oldCount != 0))
            FirstGoodie = true;
        Record.PendingExtraGoodies = 0;

        for (int episode = 1; episode <= 8; episode++)
        {
            if (!IsEpisodeAvailable(episode)) continue;
            foreach (int hinted in CareerTables.EpisodeInstructions[episode - 1]) SetGoodieInstruction(hinted);
        }
    }

    /// <summary><c>CCareer::IsEpisodeAvailable</c> (0x00421570).</summary>
    public bool IsEpisodeAvailable(int episode) => episode switch
    {
        0 or 1 => true,
        2 => CompleteLevel(110),
        3 => CompleteLevel(231) || CompleteLevel(232),
        4 => CompleteLevel(331) || CompleteLevel(332),
        5 => CompleteLevel(431) || CompleteLevel(432),
        6 => CompleteLevel(521) || CompleteLevel(522) || CompleteLevel(523) || CompleteLevel(524),
        7 => CompleteLevel(621) || CompleteLevel(622),
        8 => CompleteLevel(741) || CompleteLevel(742),
        _ => false,
    };

    /// <summary><c>CCareer::GetAndResetGoodieNewCount</c> (0x00421550).</summary>
    public int GetAndResetGoodieNewCount()
    {
        int count = NewGoodieCount;
        NewGoodieCount = 0;
        return count;
    }

    /// <summary><c>CCareer::GetAndResetFirstGoodie</c> (0x00421560).</summary>
    public bool GetAndResetFirstGoodie()
    {
        bool first = FirstGoodie;
        FirstGoodie = false;
        return first;
    }

    // --- Slots and screen position ----------------------------------------------------------------------------

    /// <summary><c>CCareer::GetSlot</c>: slots 0..255 (32 words, of which only the first 8 are reachable).</summary>
    public bool GetSlot(int slot)
    {
        if (slot < 0 || slot >= SlotRange) return false;
        int bit = slot & 31;
        int mask = 1;
        if (bit > 0) mask <<= bit;
        return (Record.SlotWord(slot >> 5) & mask) != 0;
    }

    /// <summary><c>CCareer::SetSlot</c> (0x004214e0): only a value of exactly 1 sets the slot.</summary>
    public void SetSlot(int slot, int value)
    {
        if (slot < 0 || slot >= SlotRange) return;
        int bit = slot & 31;
        int mask = 1;
        if (bit > 0) mask <<= bit;
        int current = Record.SlotWord(slot >> 5);
        Record.SetSlotWord(slot >> 5, value == 1 ? current | mask : current & ~mask);
    }

    /// <summary><c>CCareer::GetScreenPosX</c> (0x004218f0): the top byte of the aircraft counter, less 128.</summary>
    public int GetScreenPosX() => (int)((uint)Record.KilledThings((int)KillType.Aircraft) >> 24) - 128;

    /// <summary><c>CCareer::GetScreenPosY</c> (0x00421900): the top byte of the vehicle counter, less 128.</summary>
    public int GetScreenPosY() => (int)((uint)Record.KilledThings((int)KillType.Vehicles) >> 24) - 128;

    /// <summary><c>CCareer::SetScreenPosX</c> (0x00421910).</summary>
    public void SetScreenPosX(int x) => Record.SetKilledThings((int)KillType.Aircraft,
        unchecked(((x - 128) << 24) + (Record.KilledThings((int)KillType.Aircraft) & 0xffffff)));

    /// <summary><c>CCareer::SetScreenPosY</c> (0x00421940).</summary>
    public void SetScreenPosY(int y) => Record.SetKilledThings((int)KillType.Vehicles,
        unchecked(((y - 128) << 24) + (Record.KilledThings((int)KillType.Vehicles) & 0xffffff)));

    private int RequiredNode(int worldNumber)
    {
        int node = GetNodeFromWorldNo(worldNumber);
        if (node < 0) throw new InvalidOperationException($"The career has no node for world {worldNumber}.");
        return node;
    }
}

/// <summary><c>EKilledType</c>, the five scored kill counters.</summary>
public enum KillType
{
    Aircraft = 0,
    Vehicles = 1,
    Emplacements = 2,
    Infantry = 3,
    Mechs = 4,
}
