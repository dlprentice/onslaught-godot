// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Career;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Career;

public sealed class CareerTests
{
    /// <summary>The repository's one tracked career save, found from the test output folder upwards.</summary>
    private static string FixturePath
    {
        get
        {
            for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            {
                string candidate = Path.Combine(folder.FullName, "tests_shared", "fixtures", "gold_career_save.bin");
                if (File.Exists(candidate)) return candidate;
            }
            throw new FileNotFoundException("tests_shared/fixtures/gold_career_save.bin was not found above the test output.");
        }
    }

    private static OnslaughtRebuild.Parity.Career.Career NewCareer() => OnslaughtRebuild.Parity.Career.Career.Constructed();

    [Fact]
    public void TheSaveIsTheStampTheCareerTheBindingsAndTheOptions()
    {
        Assert.Equal(unchecked((short)0x4BD1), CareerSaveGame.VersionStamp);
        Assert.Equal(10_004, CareerSaveGame.Size);
        Assert.Equal(2 + 9_404 + 16 * 32 + 86, CareerSaveGame.Size);
    }

    [Fact]
    public void TheGoldCareerRoundTripsByteForByte()
    {
        byte[] original = File.ReadAllBytes(FixturePath);
        var career = NewCareer();
        CareerLoadResult loaded = CareerSaveGame.Load(career, original, suggestNextLevel: false);
        Assert.True(loaded.Loaded);
        Assert.Equal(original, CareerSaveGame.Save(career, loaded.Bindings!, loaded.Options!));
    }

    [Fact]
    public void SavingMarksTheCareerInProgressAndChangesOnlyThatFlag()
    {
        byte[] original = File.ReadAllBytes(FixturePath);
        BinaryPrimitives.WriteInt32LittleEndian(original.AsSpan(2 + 9_352), 0);
        var career = NewCareer();
        CareerLoadResult loaded = CareerSaveGame.Load(career, original, suggestNextLevel: false);
        byte[] saved = CareerSaveGame.SaveAndMarkInProgress(career, loaded.Bindings!, loaded.Options!);
        int[] changed = Enumerable.Range(0, saved.Length).Where(i => saved[i] != original[i]).ToArray();
        Assert.Equal([2 + 9_352], changed);
        Assert.Equal(1, saved[2 + 9_352]);
    }

    [Fact]
    public void AWrongVersionIsRefusedAndChangesNothing()
    {
        byte[] file = File.ReadAllBytes(FixturePath);
        file[0] ^= 1;
        var career = NewCareer();
        byte[] before = career.Record.Bytes.ToArray();
        Assert.False(CareerSaveGame.Load(career, file, suggestNextLevel: true).Loaded);
        Assert.Equal(before, career.Record.Bytes.ToArray());
    }

    [Fact]
    public void LoadingACareerToPlayKeepsTheVolumesInUseAndReadsNoOptions()
    {
        byte[] file = File.ReadAllBytes(FixturePath);
        var career = NewCareer();
        career.Record.SoundVolume = 0.25f;
        career.Record.MusicVolume = 0.5f;
        CareerLoadResult loaded = CareerSaveGame.Load(career, file, suggestNextLevel: true);
        Assert.True(loaded.Loaded);
        Assert.Null(loaded.Options);
        Assert.Equal(0.25f, career.Record.SoundVolume);
        Assert.Equal(0.5f, career.Record.MusicVolume);
    }

    [Fact]
    public void AnOutOfRangeScreenPositionIsResetOnLoad()
    {
        byte[] file = File.ReadAllBytes(FixturePath);
        var probe = NewCareer();
        CareerSaveGame.Load(probe, file, suggestNextLevel: true);
        probe.SetScreenPosX(100);
        probe.SetScreenPosY(-3);
        Assert.Equal(100, probe.GetScreenPosX());
        Assert.Equal(-3, probe.GetScreenPosY());
        probe.Record.Bytes.CopyTo(file.AsSpan(2));
        var career = NewCareer();
        CareerSaveGame.Load(career, file, suggestNextLevel: true);
        Assert.Equal(0, career.GetScreenPosX());
        Assert.Equal(-3, career.GetScreenPosY());
        Assert.Equal(probe.GetNumKilled(KillType.Aircraft), career.GetNumKilled(KillType.Aircraft));
    }

    [Fact]
    public void BlankRebuildsTheCampaignAndKeepsEverythingElse()
    {
        byte[] file = File.ReadAllBytes(FixturePath);
        var career = NewCareer();
        CareerSaveGame.Load(career, file, suggestNextLevel: true);
        career.Record.SetNodeWorldNumber(60, 1234);
        career.Record.SetLinkToNode(150, 7);
        float sound = career.Record.SoundVolume;
        career.Blank();
        Assert.Equal(1234, career.Record.NodeWorldNumber(60));
        Assert.Equal(7, career.Record.LinkToNode(150));
        Assert.Equal(sound, career.Record.SoundVolume);
        Assert.Equal(100, career.Record.NodeWorldNumber(0));
        Assert.Equal(800, career.Record.NodeWorldNumber(42));
        Assert.Equal(1, career.Record.LinkToNode(0));
        Assert.Equal(-1, career.Record.LinkToNode(1));
        Assert.Equal(-1f, career.Record.NodeRanking(0));
        Assert.Equal(0, career.Record.CareerInProgress);
        Assert.Equal(CareerGoodieState.Instructions, career.Record.GoodieState(0));
        Assert.Equal(CareerGoodieState.Unknown, career.Record.GoodieState(2));
    }

    [Fact]
    public void WinningLevel100CompletesItsNodeAndLinkAndUnlocksItsGoodies()
    {
        var career = NewCareer();
        career.Blank();
        var end = new EndLevelData { WorldFinished = 100, FinalState = EndLevelState.Won, Ranking = 0.8f };
        end.ThingsKilled[(int)KillType.Infantry] = 12;
        end.Slots[0] = 5;
        career.Update(end);
        Assert.Equal(1, career.Record.NodeCompleteFlag(0));
        Assert.Equal(0.8f, career.Record.NodeRanking(0));
        Assert.Equal(CareerLinkType.Complete, career.Record.LinkType(0));
        Assert.Equal(CareerLinkType.NotComplete, career.Record.LinkType(1));
        Assert.Equal(1, career.Record.CareerInProgress);
        Assert.Equal(5, career.Record.SlotWord(0));
        Assert.Equal(0, career.GetNumKilled(KillType.Infantry));
        Assert.Equal(CareerGoodieState.New, career.Record.GoodieState(0));
        Assert.Equal(CareerGoodieState.New, career.Record.GoodieState(8));
        Assert.Equal(CareerGoodieState.New, career.Record.GoodieState(78));
        Assert.Equal(CareerGoodieState.New, career.Record.GoodieState(121));
        Assert.Equal(CareerGoodieState.New, career.Record.GoodieState(164));
        Assert.Equal(5, career.GetAndResetGoodieNewCount());
        Assert.Equal(0, career.GetAndResetGoodieNewCount());
        Assert.True(career.GetAndResetFirstGoodie());
        Assert.False(career.GetAndResetFirstGoodie());
    }

    [Fact]
    public void ALowerRankingNeverReplacesABetterOne()
    {
        var career = NewCareer();
        career.Blank();
        career.Update(new EndLevelData { WorldFinished = 100, FinalState = EndLevelState.Won, Ranking = 0.9f });
        career.Update(new EndLevelData { WorldFinished = 100, FinalState = EndLevelState.Won, Ranking = 0.3f });
        Assert.Equal(0.9f, career.Record.NodeRanking(0));
    }

    [Fact]
    public void ALostLevelOnlyRechecksGoodies()
    {
        var career = NewCareer();
        career.Blank();
        byte[] before = career.Record.Bytes.ToArray();
        career.Update(new EndLevelData { WorldFinished = 100, FinalState = EndLevelState.Lost, Ranking = 0.9f });
        Assert.Equal(before, career.Record.Bytes.ToArray());
    }

    [Fact]
    public void ASecondRouteIntoANodeBreaksTheFirst()
    {
        var career = NewCareer();
        career.Blank();
        // Worlds 211 (node 3) and 212 (node 4) both lead to world 221 (node 5) by their lower links (6 and 8).
        career.Update(new EndLevelData { WorldFinished = 211, FinalState = EndLevelState.Won, Ranking = 0.5f });
        Assert.Equal(CareerLinkType.Complete, career.Record.LinkType(6));
        career.Update(new EndLevelData { WorldFinished = 212, FinalState = EndLevelState.Won, Ranking = 0.5f });
        Assert.Equal(CareerLinkType.Complete, career.Record.LinkType(8));
        Assert.Equal(CareerLinkType.CompleteBroken, career.Record.LinkType(6));
    }

    [Fact]
    public void AHigherLinkNeedsEverySecondaryObjective()
    {
        var career = NewCareer();
        career.Blank();
        var partial = new EndLevelData { WorldFinished = 200, FinalState = EndLevelState.Won, Ranking = 0.5f };
        partial.SecondaryObjectives[0] = ObjectiveState.Complete;
        partial.SecondaryObjectives[1] = ObjectiveState.Failed;
        career.Update(partial);
        Assert.Equal(CareerLinkType.Complete, career.Record.LinkType(4));
        Assert.Equal(CareerLinkType.NotComplete, career.Record.LinkType(5));
        var full = new EndLevelData { WorldFinished = 200, FinalState = EndLevelState.Won, Ranking = 0.5f };
        full.SecondaryObjectives[0] = ObjectiveState.Complete;
        career.Update(full);
        Assert.Equal(CareerLinkType.Complete, career.Record.LinkType(5));
    }

    [Theory]
    [InlineData(1f, 'S')]
    [InlineData(0f, 'E')]
    [InlineData(-1f, 'E')]
    [InlineData(0.1f, 'D')]
    [InlineData(0.25f, 'C')]
    [InlineData(0.5f, 'B')]
    [InlineData(0.99f, 'A')]
    [InlineData(1.1f, '@')]
    [InlineData(float.NaN, 'D')]
    [InlineData(float.Epsilon, 'D')]
    [InlineData(0.24999999f, 'D')]
    [InlineData(float.MaxValue, 'D')]
    [InlineData(float.PositiveInfinity, 'D')]
    [InlineData(17f, '\0')]
    public void GradesFollowTheGameRule(float ranking, char grade) =>
        Assert.Equal((sbyte)grade, OnslaughtRebuild.Parity.Career.Career.GradeFromRanking(ranking));

    [Fact]
    public void NegativeZeroIsAnEGrade() =>
        Assert.Equal((sbyte)'E', OnslaughtRebuild.Parity.Career.Career.GradeFromRanking(BitConverter.Int32BitsToSingle(unchecked((int)0x80000000))));

    [Fact]
    public void SBeatsEveryGradeAndALowerLetterIsBetter()
    {
        Assert.True(OnslaughtRebuild.Parity.Career.Career.GradeAtLeast((sbyte)'S', (sbyte)'A'));
        Assert.False(OnslaughtRebuild.Parity.Career.Career.GradeAtLeast((sbyte)'A', (sbyte)'S'));
        Assert.True(OnslaughtRebuild.Parity.Career.Career.GradeAtLeast((sbyte)'B', (sbyte)'C'));
        Assert.False(OnslaughtRebuild.Parity.Career.Career.GradeAtLeast((sbyte)'D', (sbyte)'C'));
        Assert.False(OnslaughtRebuild.Parity.Career.Career.GradeAtLeast((sbyte)'E', (sbyte)'C'));
    }

    [Fact]
    public void SlotsAndBaseThingsSetABitOnlyForExactlyOne()
    {
        var career = NewCareer();
        career.SetSlot(33, 1);
        Assert.True(career.GetSlot(33));
        Assert.Equal(2, career.Record.SlotWord(1));
        career.SetSlot(33, 2);
        Assert.False(career.GetSlot(33));
        career.SetNodeBaseThingExists(0, 40, 0);
        Assert.False(career.NodeBaseThingExists(0, 40));
        career.SetNodeBaseThingExists(0, 40, 1);
        Assert.True(career.NodeBaseThingExists(0, 40));
    }
}
