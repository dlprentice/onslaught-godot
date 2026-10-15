// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class VirtualKeyboardTests
{
    // A synthetic monospaced font: 10 pixels a character, no glyph for the accented capitals E-circumflex and I-umlaut.
    private sealed class TestFont : IFontMetrics
    {
        public int TextWidth(string text) => text.Length * 10;
        public bool CanDisplay(char character) => character is not ((char)0xca or (char)0xcf);
    }

    private static (FrontEnd FrontEnd, VirtualKeyboardPage Keyboard) NewKeyboard(params string[] saves)
    {
        FrontEnd frontEnd = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        frontEnd.Career.Blank();
        foreach (string save in saves) frontEnd.MemoryCard.Folder.Write(save, new byte[10004]);
        VirtualKeyboardPage keyboard = new(frontEnd) { Font = new TestFont() };
        frontEnd[FrontEndPage.VirtualKeyboard] = keyboard;
        frontEnd.VirtualKeyboard = keyboard;
        return (frontEnd, keyboard);
    }

    [Fact]
    public void TheDefaultNameIsTheFirstFreeBeaNumberComparedExactly()
    {
        (_, VirtualKeyboardPage keyboard) = NewKeyboard("BEA 1", "BEA 3");
        keyboard.SetDefaultName();
        Assert.Equal("BEA 2", keyboard.Name);
        Assert.Equal(5, keyboard.Position);
        Assert.True(keyboard.Fresh);
        // wcscmp: a save named in another case does not clash.
        (_, keyboard) = NewKeyboard("bea 1");
        keyboard.SetDefaultName();
        Assert.Equal("BEA 1", keyboard.Name);
    }

    [Fact]
    public void TheDefaultNameSkipsEveryTakenNumber()
    {
        (_, VirtualKeyboardPage keyboard) = NewKeyboard("BEA 1", "BEA 2", "BEA 4");
        keyboard.SetDefaultName();
        Assert.Equal("BEA 3", keyboard.Name);
        Assert.Equal(5, keyboard.Position);
        Assert.True(keyboard.Fresh);
    }

    [Fact]
    public void EnterAcceptsTheNameAndGoesToTheSavePage()
    {
        (FrontEnd frontEnd, VirtualKeyboardPage keyboard) = NewKeyboard();
        frontEnd[FrontEndPage.Main] = new MainPage(frontEnd);
        frontEnd.Begin(FrontEndPage.Main);
        keyboard.SetDefaultName();
        frontEnd.SetPage(FrontEndPage.VirtualKeyboard, 0);
        frontEnd.Step();
        frontEnd.Step();
        Assert.Equal(FrontEndPage.VirtualKeyboard, frontEnd.ActivePage);
        Assert.Equal(keyboard.TrapHandler, frontEnd.Platform.KeyTrap);

        frontEnd.Platform.PostTapeKey(0x0D, Dik.Return, down: true);
        frontEnd.Step();
        Assert.Equal("BEA 1", frontEnd.SaveGameName);
        Assert.True(frontEnd.SaveMode);
        Assert.Equal(AutoSaveMode.Not, frontEnd.AutoSave);
        Assert.Equal(FrontEndPage.SaveGame, frontEnd.TransitionTo);
        Assert.Equal(20f, frontEnd.TransitionTime);
        Assert.Equal(FrontEndSound.Select, frontEnd.SoundsPlayed[^1]);
        // The trapped key never reaches the key state, so the controllers do not see it.
        Assert.False(frontEnd.Platform.KeyOn(Dik.Return));
        // Leaving the page removes the trap.
        frontEnd.Step();
        Assert.Null(frontEnd.Platform.KeyTrap);
    }

    [Fact]
    public void TypingReplacesTheFreshNameThenInsertsAtTheCursor()
    {
        (_, VirtualKeyboardPage keyboard) = NewKeyboard();
        keyboard.SetDefaultName();
        keyboard.HandleKey('A');
        keyboard.HandleKey('C');
        keyboard.HandleKey(VirtualKeyboardPage.Left);
        keyboard.HandleKey('B');
        Assert.Equal("ABC", keyboard.Name);
        Assert.Equal(2, keyboard.Position);
        keyboard.HandleKey(VirtualKeyboardPage.Backspace);
        Assert.Equal(("AC", 1), (keyboard.Name, keyboard.Position));
        // A missing accented capital falls back to the plain letter; one with a glyph is kept.
        keyboard.HandleKey((char)0xca);
        keyboard.HandleKey((char)0xcb);
        Assert.Equal("AEËC", keyboard.Name);
    }

    [Fact]
    public void BackspaceOnTheFreshNameClearsItAndTheNameStopsAt31()
    {
        (_, VirtualKeyboardPage keyboard) = NewKeyboard();
        keyboard.SetDefaultName();
        keyboard.HandleKey(VirtualKeyboardPage.Backspace);
        Assert.Equal(("", 0, false), (keyboard.Name, keyboard.Position, keyboard.Fresh));
        for (int i = 0; i < 40; i++) keyboard.HandleKey('x');
        Assert.Equal(31, keyboard.Name.Length);
        keyboard.HandleKey(VirtualKeyboardPage.Right);
        Assert.Equal(31, keyboard.Position);
    }

    [Fact]
    public void MovingBetweenRowsKeepsTheHorizontalPoint()
    {
        (_, VirtualKeyboardPage keyboard) = NewKeyboard();
        keyboard.SetDefaultName();
        keyboard.TransitionNotification(FrontEndPage.Main);
        Assert.Equal((0, 0), (keyboard.Row, keyboard.Column));
        keyboard.ButtonPressed(FrontEndButtons.MenuDown, 1f);
        for (int i = 0; i < 5; i++) keyboard.ButtonPressed(FrontEndButtons.MenuRight, 1f);
        Assert.Equal((1, 5), (keyboard.Row, keyboard.Column));
        keyboard.ButtonPressed(FrontEndButtons.MenuDown, 1f);
        keyboard.ButtonPressed(FrontEndButtons.MenuDown, 1f);
        Assert.Equal((3, 5, 0.5f), (keyboard.Row, keyboard.Column, keyboard.ColumnFraction));
        // 'Y' is centred at 166 + 4.5 * 34 = 319, three quarters across the 204-wide space bar.
        keyboard.ButtonPressed(FrontEndButtons.MenuDown, 1f);
        Assert.Equal((4, 1, 0.75f), (keyboard.Row, keyboard.Column, keyboard.ColumnFraction));
        keyboard.ButtonPressed(FrontEndButtons.MenuUp, 1f);
        Assert.Equal((3, 5, 0.5f), (keyboard.Row, keyboard.Column, keyboard.ColumnFraction));
        // Right passes Z and backspace, then wraps to the row's first key.
        for (int i = 0; i < 3; i++) keyboard.ButtonPressed(FrontEndButtons.MenuRight, 1f);
        Assert.Equal(0, keyboard.Column);
    }

    [Fact]
    public void DoneIsSkippedWhileTheNameIsBlank()
    {
        (_, VirtualKeyboardPage keyboard) = NewKeyboard();
        keyboard.SetDefaultName();
        keyboard.TransitionNotification(FrontEndPage.Main);
        keyboard.ButtonPressed(FrontEndButtons.MenuDown, 1f);
        keyboard.HandleKey(VirtualKeyboardPage.Backspace);
        keyboard.HandleKey(' ');
        // Up from row 1 lands on Done, which is disabled, so it carries on upward and wraps to row 4.
        keyboard.ButtonPressed(FrontEndButtons.MenuUp, 1f);
        Assert.Equal((4, 0), (keyboard.Row, keyboard.Column));
    }
}
