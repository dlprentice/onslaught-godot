// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Tests.Career;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class LoadPathTests
{
    private sealed class Rig
    {
        public FrontEnd FrontEnd { get; } = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        public MainPage Main { get; }
        public DirectoryPage Directory { get; }
        public LoadGamePage Load { get; }
        public VirtualKeyboardPage Keyboard { get; }
        public List<byte[]> OptionsWritten { get; } = [];

        public Rig(params (string Name, byte[] Bytes)[] saves)
        {
            FrontEnd.Career.Blank();
            foreach ((string name, byte[] bytes) in saves) FrontEnd.MemoryCard.Folder.Write(name, bytes);
            FrontEnd.WriteDefaultOptions = OptionsWritten.Add;
            Main = new MainPage(FrontEnd);
            Directory = new DirectoryPage(FrontEnd);
            Load = new LoadGamePage(FrontEnd);
            Keyboard = new VirtualKeyboardPage(FrontEnd);
            DevSelectPage devSelect = new(FrontEnd);
            LevelSelectPage levelSelect = new(FrontEnd);
            FrontEnd[FrontEndPage.Main] = Main;
            FrontEnd[FrontEndPage.Directory] = Directory;
            FrontEnd[FrontEndPage.LoadGame] = Load;
            FrontEnd[FrontEndPage.VirtualKeyboard] = Keyboard;
            FrontEnd[FrontEndPage.DevSelect] = devSelect;
            FrontEnd[FrontEndPage.LevelSelect] = levelSelect;
            FrontEnd.Directory = Directory;
            FrontEnd.LoadGame = Load;
            FrontEnd.VirtualKeyboard = Keyboard;
            FrontEnd.DevSelect = devSelect;
            FrontEnd.LevelSelect = levelSelect;
            FrontEnd.Begin(FrontEndPage.Main);
        }

        public void Key(int vk, int scan)
        {
            FrontEnd.Platform.PostTapeKey(vk, scan, down: true);
            FrontEnd.Step();
            FrontEnd.Platform.PostTapeKey(vk, scan, down: false);
            FrontEnd.Step();
        }

        public void RunUntil(FrontEndPage page)
        {
            for (int i = 0; FrontEnd.ActivePage != page; i++)
            {
                Assert.True(i < 300, $"never reached {page}");
                FrontEnd.Step();
            }
        }

        // Main menu: Load (Continue is skipped without a career), then the device page goes on to the list.
        public void OpenTheList()
        {
            Key(0x28, Dik.Down);
            Assert.Equal(2, Main.Selected);
            Key(0x0D, Dik.Return);
            RunUntil(FrontEndPage.Directory);
        }
    }

    [Fact]
    public void LoadingASaveFromTheListLoadsTheCareerAndGoesToTheLevelSelect()
    {
        byte[] gold = File.ReadAllBytes(CareerTests.FixturePath);
        Rig r = new(("First", gold), ("Second", gold));
        r.OpenTheList();
        Assert.Equal((2, "First", "Second"), (r.Directory.NumSaves, r.Directory.Save(0), r.Directory.Save(1)));
        r.Key(0x28, Dik.Down);
        Assert.Equal(1, r.Directory.Cursor);
        r.Key(0x0D, Dik.Return);
        r.RunUntil(FrontEndPage.LevelSelect);

        OnslaughtRebuild.Parity.Career.Career expected = new(CareerRecord.Constructed());
        Assert.True(CareerSaveGame.Load(expected, gold, suggestNextLevel: true).Loaded);
        Assert.Equal(expected.Record.Bytes.ToArray(), r.FrontEnd.Career.Record.Bytes.ToArray());
        Assert.Equal((1, "Second"), (r.Load.SaveGameNumber, r.FrontEnd.SaveGameName));
        Assert.Equal(("Second", 6), (r.Keyboard.Name, r.Keyboard.Position));
        Assert.Equal(AutoSaveMode.Normal, r.FrontEnd.AutoSave);
        Assert.Equal(LoadGamePage.Text.LoadDone, r.Load.LastText);
        Assert.Equal(gold, r.OptionsWritten.Single());
    }

    [Fact]
    public void AWrongVersionOrAShortFileReturnsToTheMainMenu()
    {
        byte[] gold = File.ReadAllBytes(CareerTests.FixturePath);
        byte[] wrongVersion = (byte[])gold.Clone();
        wrongVersion[0] ^= 1;
        foreach ((byte[] bytes, LoadGamePage.Text text) in new[] { (wrongVersion, LoadGamePage.Text.BadSaveVersion), (gold[..100], LoadGamePage.Text.LoadInvalid) })
        {
            Rig r = new(("Bad", bytes));
            byte[] before = r.FrontEnd.Career.Record.Bytes.ToArray();
            r.OpenTheList();
            r.Key(0x0D, Dik.Return);
            r.RunUntil(FrontEndPage.Main);
            Assert.Equal(text, r.Load.LastText);
            Assert.Equal(before, r.FrontEnd.Career.Record.Bytes.ToArray());
            Assert.Empty(r.OptionsWritten);
        }
    }

    [Fact]
    public void ClickingARowPicksThatSave()
    {
        byte[] gold = File.ReadAllBytes(CareerTests.FixturePath);
        Rig r = new(("A", gold), ("B", gold), ("C", gold));
        r.OpenTheList();
        // The third row is at y 130 + 2 * 24.
        r.FrontEnd.Platform.PostMouseMessage(MouseMessage.Move, 200, 190, 0);
        r.FrontEnd.Platform.PostMouseMessage(MouseMessage.LeftDown, 200, 190, 0x1);
        r.FrontEnd.Platform.PostMouseMessage(MouseMessage.LeftUp, 200, 190, 0);
        r.FrontEnd.Step();
        Assert.Equal(2, r.Load.SaveGameNumber);
        Assert.Equal(FrontEndPage.LoadGame, r.FrontEnd.ActivePage);
    }

    [Fact]
    public void DraggingInTheScrollBarMovesTheCursorAndTheList()
    {
        byte[] gold = File.ReadAllBytes(CareerTests.FixturePath);
        Rig r = new([.. Enumerable.Range(0, 30).Select(i => ($"S{i:00}", gold))]);
        r.OpenTheList();
        r.FrontEnd.Platform.PostMouseMessage(MouseMessage.Move, 520, 300, 0);
        r.FrontEnd.Platform.PostMouseMessage(MouseMessage.LeftDown, 520, 300, 0x1);
        r.FrontEnd.Step();
        // The bar is 11 rows of 261/30 = 95.7 high: (300 - 130 - 47.85) / (271 - 95.7) * 29.9 = 20.83 rounds to 21 (fistp
        // under /QIfist, 0051b3cb), and the list starts at (300 - 130) / 271 * 19.9 = 12.48, rounded to 12 (0051b41a).
        Assert.Equal((21, 12), (r.Directory.Cursor, r.Directory.FirstShown));
    }
}
