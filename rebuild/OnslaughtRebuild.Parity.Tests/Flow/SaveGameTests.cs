// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Pages;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class SaveGameTests
{
    private sealed class Rig
    {
        public FrontEnd FrontEnd { get; }
        public SaveGamePage Save { get; }
        public VirtualKeyboardPage Keyboard { get; }

        public Rig(AutoSaveMode autoSave, params string[] existing)
        {
            FrontEnd = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
            FrontEnd.Career.Blank();
            FrontEnd.CurrentOptions = () => CareerOptions.Read(new byte[CareerOptions.Size]);
            foreach (string name in existing) FrontEnd.MemoryCard.Folder.Write(name, new byte[3]);
            Save = new SaveGamePage(FrontEnd);
            Keyboard = new VirtualKeyboardPage(FrontEnd);
            FrontEnd[FrontEndPage.Main] = new MainPage(FrontEnd);
            FrontEnd[FrontEndPage.SaveGame] = Save;
            FrontEnd[FrontEndPage.VirtualKeyboard] = Keyboard;
            FrontEnd.VirtualKeyboard = Keyboard;
            FrontEnd.Begin(FrontEndPage.Main);
            Keyboard.SetDefaultName();
            FrontEnd.SaveGameName = "BEA 1";
            FrontEnd.AutoSave = autoSave;
            FrontEnd.SetSuccessPage(FrontEndPage.LevelSelect, 0);
            FrontEnd.SetPage(FrontEndPage.SaveGame, 0);
        }
    }

    [Fact]
    public void ANewCareerIsSavedThenTheSuccessPageFollows()
    {
        Rig r = new(AutoSaveMode.Not);
        r.FrontEnd.Step();
        byte[] file = r.FrontEnd.MemoryCard.Folder.Read("BEA 1");
        Assert.Equal(CareerSaveGame.Size, file.Length);
        Assert.Equal(CareerSaveGame.VersionStamp, BinaryPrimitives.ReadInt16LittleEndian(file));
        Assert.Equal(1, r.FrontEnd.Career.Record.CareerInProgress);
        Assert.False(r.Save.CareerWasInProgress);
        Assert.Equal(SaveGamePage.Text.FreshSaveDone, r.Save.LastText);
        Assert.True(r.FrontEnd.MessageBox.BeingDisplayed);
        Assert.Equal(AutoSaveMode.Normal, r.FrontEnd.AutoSave);
        Assert.False(r.Keyboard.Fresh);
        Assert.Equal(FrontEndPage.LevelSelect, r.FrontEnd.ActivePage);

        // The saved career loads back as the one saved, except that a load to play keeps its own two volumes, and a
        // blank career's screen position, packed into the top bytes of two kill counts with no kills, reads -128:
        // out of range, so the load resets it to 0 (byte 0x80 at the top of each count).
        OnslaughtRebuild.Parity.Career.Career loaded = new(CareerRecord.Constructed());
        Assert.True(CareerSaveGame.Load(loaded, file, suggestNextLevel: true).Loaded);
        Assert.Equal((-128, -128), (r.FrontEnd.Career.GetScreenPosX(), r.FrontEnd.Career.GetScreenPosY()));
        Assert.Equal((0, 0), (loaded.GetScreenPosX(), loaded.GetScreenPosY()));
        byte[] saved = r.FrontEnd.Career.Record.Bytes.ToArray(), back = loaded.Record.Bytes.ToArray();
        int[] differing = [.. Enumerable.Range(0, saved.Length).Where(k => saved[k] != back[k])];
        Assert.All(differing, k => Assert.True(k is >= 9204 and < 9224 || k is >= 9356 and < 9364, $"byte {k} differs"));
        Assert.Equal(2, differing.Count(k => k < 9224));
    }

    [Fact]
    public void AUsedNameAsksAndYesReplacesTheSave()
    {
        Rig r = new(AutoSaveMode.Not, "bea 1");
        r.FrontEnd.Step();
        Assert.Equal(MessageBoxQuestion.Overwrite, r.FrontEnd.MessageBox.Question);
        Assert.Equal((0, "BEA 1"), (r.Save.ExistingSave, r.Save.ExistingSaveName));
        Assert.Equal(3, r.FrontEnd.MemoryCard.Folder.Read("bea 1").Length);
        for (int i = 0; i < 100 && r.FrontEnd.MessageBox.FadeState != 1; i++) r.FrontEnd.Step();
        r.FrontEnd.MessageBox.ButtonPressed(FrontEndButtons.MenuUp, 1f);
        r.FrontEnd.MessageBox.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        r.FrontEnd.Step();
        Assert.Equal(["BEA 1"], r.FrontEnd.MemoryCard.Folder.Names);
        Assert.Equal(CareerSaveGame.Size, r.FrontEnd.MemoryCard.Folder.Read("BEA 1").Length);
        Assert.Equal(FrontEndPage.LevelSelect, r.FrontEnd.ActivePage);
    }

    [Fact]
    public void AUsedNameAnsweredNoGoesBackToTheKeyboard()
    {
        Rig r = new(AutoSaveMode.Not, "BEA 1");
        r.FrontEnd.Step();
        for (int i = 0; i < 100 && r.FrontEnd.MessageBox.FadeState != 1; i++) r.FrontEnd.Step();
        r.FrontEnd.MessageBox.ButtonPressed(FrontEndButtons.MenuSelect, 1f);
        r.FrontEnd.Step();
        Assert.Equal(-1, r.Save.ExistingSave);
        Assert.Equal(FrontEndPage.VirtualKeyboard, r.FrontEnd.TransitionTo);
        Assert.Equal(20f, r.FrontEnd.TransitionTime);
        Assert.Equal(3, r.FrontEnd.MemoryCard.Folder.Read("BEA 1").Length);
    }

    [Fact]
    public void AnAutosaveOverwritesItsOwnSaveWithoutAsking()
    {
        Rig r = new(AutoSaveMode.Normal, "BEA 1");
        r.FrontEnd.Career.Record.CareerInProgress = 1;
        r.FrontEnd.Step();
        Assert.Equal(CareerSaveGame.Size, r.FrontEnd.MemoryCard.Folder.Read("BEA 1").Length);
        Assert.Equal(SaveGamePage.Text.SaveDone, r.Save.LastText);
        Assert.Equal(FrontEndPage.LevelSelect, r.FrontEnd.ActivePage);
    }

    [Fact]
    public void ThePretendAutosaveNeverSaves()
    {
        Rig r = new(AutoSaveMode.Pretend);
        Assert.Throws<InvalidOperationException>(() => r.FrontEnd.Step());
    }

    [Fact]
    public void TheCheatCodesDecodeAndMatchInsideTheSaveName()
    {
        string[] codes = [.. Enumerable.Range(0, 6).Select(i => new string([.. SaveGamePage.CheatCode(i).Select(b => (char)b)]))];
        Assert.Equal(["MALLOY", "TURKEY", "V3R5IOF", "Maladim", "Aurore", "latête"], codes);

        Rig r = new(AutoSaveMode.Not);
        r.FrontEnd.SaveGameName = "Mr MALLOY 2";
        Assert.True(r.Save.IsCheatActive(0));
        Assert.False(r.Save.IsCheatActive(1));
        r.FrontEnd.SaveGameName = "malloy";
        Assert.False(r.Save.IsCheatActive(0));
        // FromWCHAR keeps the low byte: U+0100 ends the name there.
        r.FrontEnd.SaveGameName = "AĀMALLOY";
        Assert.False(r.Save.IsCheatActive(0));
        r.Save.AllCheatsEnabled = true;
        Assert.True(r.Save.IsCheatActive(5));
    }
}
