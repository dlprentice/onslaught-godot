// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/FEPSaveGame.cpp (CFEPSaveGame), in Stuart's release.

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Numerics;
using Fet = OnslaughtRebuild.Parity.Text.FrontEndText;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The save page (<c>CFEPSaveGame</c>): while it is active and no box is up, it saves the career under the name the
/// keyboard chose (or the career's own name, for an autosave), then shows "saved" for 60 frames and goes to the success
/// page. A name already used asks to overwrite, unless this is an autosave.
/// </summary>
/// <remarks>
/// The PC's card is always present and formatted, never changes and has all the space it wants; its create, write and
/// delete return only success, failure or "file exists". So the format question, the "card removed" complaint, the
/// autosave-scared refusal and the card-full results (<c>MCE_NOFILE</c>, <c>MCE_CARDFULL</c>,
/// <c>MCE_TOO_MANY_SAVES</c>) are not reachable and not ported. Asking to delete saves needs more than 4,095 saves.
/// </remarks>
public sealed class SaveGamePage(FrontEnd frontEnd) : FrontEndPageBase
{
    /// <summary>Message box texts this page shows, for the host's renderer (<c>FEX_*</c> and string ids).</summary>
    public enum Text
    {
        SaveDone,
        FreshSaveDone,
        SavedFromOptions,
        SaveError,
        CouldNotDelete,
        TooManySaves,
        Overwrite,
    }

    public int CX { get; private set; }
    public int CY { get; private set; }

    /// <summary><c>mExistingSave</c>: the save the overwrite question is about, or -1.</summary>
    public int ExistingSave { get; private set; } = -1;

    public string ExistingSaveName { get; private set; } = "";
    public bool CareerWasInProgress { get; private set; }
    public float StartTime { get; private set; }

    /// <summary>
    /// <c>gSaveFromOptions</c> (006799d0): set by the options page's save; the success text changes. CreateSave clears it
    /// on every path that reaches its end (FEPSaveGame.cpp:574; in the pristine executable 74154bfa…, four
    /// <c>mov [006799d0], bl</c> at 0x465290, 0x465350, 0x4653c5 and 0x465482 against one read at 0x465088), not on
    /// its early returns.
    /// </summary>
    public bool SaveFromOptions { get; set; }

    /// <summary>The text of the last box this page created.</summary>
    public Text? LastText { get; private set; }

    /// <summary><c>CFEPSaveGame::Init</c> (0x00464620).</summary>
    public override bool Init()
    {
        CX = 0;
        CY = 0;
        return true;
    }

    /// <summary><c>CFEPSaveGame::ButtonPressed</c> (0x00464630).</summary>
    public override void ButtonPressed(int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuUp:
                CY = Math.Max(CY - 1, 0);
                frontEnd.PlaySound(FrontEndSound.Move);
                break;
            case FrontEndButtons.MenuDown:
                CY = Math.Min(CY + 1, 0xff);
                frontEnd.PlaySound(FrontEndSound.Move);
                break;
            case FrontEndButtons.MenuLeft:
                CX = Math.Max(CX - 1, 0);
                frontEnd.PlaySound(FrontEndSound.Move);
                break;
            case FrontEndButtons.MenuRight:
                CX = Math.Min(CX + 1, 0xff);
                frontEnd.PlaySound(FrontEndSound.Move);
                break;
            case FrontEndButtons.MenuSelect:
                frontEnd.PlaySound(FrontEndSound.Select);
                break;
            case FrontEndButtons.MenuBack:
                frontEnd.PlaySound(FrontEndSound.Back);
                frontEnd.SetPage(FrontEndPage.Main, 0);
                break;
        }
    }

    /// <summary><c>CFEPSaveGame::Process</c> (0x00464730): the save while active, and the overwrite question's answer.</summary>
    public override void Process(FrontEndPageState state)
    {
        if (state == FrontEndPageState.Inactive) return;
        FrontEndMessageBox box = frontEnd.MessageBox;
        if (state == FrontEndPageState.Active && !box.BeingDisplayed) CreateSave();
        if (!box.BeingDisplayed || box.Question != MessageBoxQuestion.Overwrite) return;

        switch (box.GetFinalChoice())
        {
            case MessageBoxQuestion.Yes:
                if (ExistingSave != -1)
                {
                    if (frontEnd.MemoryCard.DeleteSave(ExistingSaveName) != MemoryCard.Success)
                    {
                        // Couldn't delete it: back to choosing a name.
                        ExistingSave = -1;
                        ExistingSaveName = "";
                        frontEnd.SaveMode = true;
                        frontEnd.SetPage(FrontEndPage.VirtualKeyboard, 20);
                        box.Kill();
                        Show(0, MessageBoxOption.AContinue, 0, Text.CouldNotDelete);
                        box.SetOnDismiss(FrontEndPage.VirtualKeyboard, 0);
                        break;
                    }
                    ExistingSave = -1;
                }
                CreateSave();
                break;
            case MessageBoxQuestion.No:
                ExistingSave = -1;
                ExistingSaveName = "";
                frontEnd.SaveMode = true;
                frontEnd.SetPage(FrontEndPage.VirtualKeyboard, 20);
                break;
        }
    }

    /// <summary>
    /// <c>CFEPSaveGame::Render</c> (0x00464a80): the borders and mask, the title, the small logo and the continue arrow (its
    /// click presses select). The title is read whether or not it is drawn (each read checks the X key).
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        frontEnd.DrawSlidingTextBordersAndMask(transition, dest);
        if (frontEnd.Texts is { } texts)
            frontEnd.DrawTitleBar(texts.GetText(OnslaughtRebuild.Parity.Text.FrontEndText.SaveGame2), transition, dest);
        frontEnd.RenderSmallBEALogo(Draw.FrontEndDraw.RangeTransition(transition, 0.75f, 1f));
        frontEnd.RenderHelpArrow(4, transition);
    }

    /// <summary><c>CFEPSaveGame::RenderPreCommon</c>: the standard video background.</summary>
    public override void RenderPreCommon(float transition, FrontEndPage dest) =>
        frontEnd.DrawStandardVideoBackground(transition, 0x3fffffff, dest);

    /// <summary><c>CFEPSaveGame::TransitionNotification</c> (0x00464b10).</summary>
    public override void TransitionNotification(FrontEndPage from) =>
        StartTime = (float)X87.Add(frontEnd.Clock.GetSysTimeFloat(), 2.0);

    private void Show(uint timeout, MessageBoxOption option, int question, Text text)
    {
        // Each box gets its layout from its text (FEPSaveGame.cpp's Create calls). The "saved" box reads the career's text
        // first, which checks the X key, even when saved from the options page, whose own text replaces it (CreateSave).
        float width = text is Text.TooManySaves or Text.Overwrite ? 300f : 400f;
        Func<OnslaughtRebuild.Parity.Text.FrontEndTexts, string>? source = text switch
        {
            Text.SaveDone or Text.FreshSaveDone or Text.SavedFromOptions => t =>
            {
                string saved = t.GetTextByName(CareerWasInProgress ? "FETX_SAVEDONE" : "FETX_FRESHSAVEDONE");
                return text == Text.SavedFromOptions ? t.PcText(0xd6) : saved;
            },
            Text.CouldNotDelete => t => t.PcText(0x76),
            Text.TooManySaves => t => t.GetText(Fet.TooManySaves),
            Text.Overwrite => t => t.GetText(Fet.OverwriteSaveGame),
            Text.SaveError => t => t.GetText(Fet.SaveError),
            _ => null,
        };
        if (source is null) frontEnd.MessageBox.Create(timeout, 10, false, option, question);
        else frontEnd.ShowMessage(320f, 240f, width, source, timeout, 10, option, question);
        LastText = text;
    }

    /// <summary>
    /// <c>CFEPSaveGame::CreateSave</c> (0x00464c50). An autosave may overwrite its own save; a new save may not, and
    /// asks first when the name (compared as <c>_wcsicmp</c> does, the last match winning) is already used.
    /// </summary>
    public void CreateSave()
    {
        MemoryCard card = frontEnd.MemoryCard;
        string name = frontEnd.SaveGameName;
        if (name.Length == 0) throw new InvalidOperationException("The save page needs a name (ASSERT(mSaveGameName[0])).");
        if (frontEnd.AutoSave == AutoSaveMode.Pretend)
            throw new InvalidOperationException("A pretend autosave never saves (the C++ asserts it).");

        // The PC's card never reports having been unplugged, so an autosave may always overwrite.
        bool allowedOverwrite = frontEnd.AutoSave == AutoSaveMode.Normal;
        int numSaves = card.NumSaves;
        (_, _, int bytesFree, _) = card.GetCardInfo(frontEnd.MemoryCardNumber);
        if ((bytesFree < card.GetSaveSize(CareerSaveGame.Size) || numSaves > 4095) && !allowedOverwrite)
        {
            // AskIfYouWantToDelete on PC: the too-many-saves text, as a delete-saves question.
            Show(0, MessageBoxOption.YesNo, MessageBoxQuestion.DeleteSaves, Text.TooManySaves);
            return;
        }

        CareerWasInProgress = frontEnd.Career.Record.CareerInProgress != 0;

        int saveNumber = -1;
        for (int i = 0; i < card.NumSaves; i++)
        {
            if (MemoryCard.WcsIEquals(card.GetSaveName(i), name)) saveNumber = i;
        }
        if (saveNumber != -1 && !allowedOverwrite)
        {
            AskToOverwrite(saveNumber, name);
            return;
        }

        int result = card.CreateSave(name, allowedOverwrite, out saveNumber);
        if (result == MemoryCard.FileExists)
        {
            AskToOverwrite(saveNumber, name);
        }
        else if (result == MemoryCard.Success)
        {
            CareerOptions options = frontEnd.CurrentOptions?.Invoke()
                ?? throw new InvalidOperationException("Saving needs the game's live options; the host must set FrontEnd.CurrentOptions.");
            byte[] file = CareerSaveGame.SaveAndMarkInProgress(frontEnd.Career, frontEnd.Controls.Bindings.SaveConfigurable(), options);
            result = card.WriteSave(name, file);
            frontEnd.MessageBox.Kill();
            if (result == MemoryCard.Success)
            {
                Text text = SaveFromOptions ? Text.SavedFromOptions : CareerWasInProgress ? Text.SaveDone : Text.FreshSaveDone;
                frontEnd.MessageBox.Kill();
                Show(60, MessageBoxOption.None, 0, text);
                frontEnd.SetPage(frontEnd.SuccessPage, frontEnd.SuccessTransitionTime);
                frontEnd.AutoSave = AutoSaveMode.Normal;
                frontEnd.VirtualKeyboard?.NameUsed();
            }
            else
            {
                SaveError();
            }
        }
        else
        {
            // The card is still there and formatted (always, on PC), so it is a save error.
            frontEnd.MessageBox.Kill();
            SaveError();
        }
        SaveFromOptions = false;
    }

    private void AskToOverwrite(int saveNumber, string name)
    {
        frontEnd.MessageBox.Kill();
        ExistingSave = saveNumber;
        ExistingSaveName = name;
        Show(0, MessageBoxOption.YesNo, MessageBoxQuestion.Overwrite, Text.Overwrite);
    }

    private void SaveError()
    {
        frontEnd.MessageBox.Kill();
        Show(0, MessageBoxOption.AContinue, 0, Text.SaveError);
        frontEnd.SetPage(FrontEndPage.Main, 0);
    }

    // The six cheat codes, each XORed with "HELP ME!!" (FEPSaveGame.cpp's cheatname and cheatkey).
    private static readonly byte[][] CheatNames =
    [
        [0x05, 0x04, 0x00, 0x1c, 0x6f, 0x14, 0x45, 0x13, 0x21],
        [0x1c, 0x10, 0x1e, 0x1b, 0x65, 0x14, 0x45],
        [0x1e, 0x76, 0x1e, 0x65, 0x69, 0x02, 0x03, 0x21],
        [0x05, 0x24, 0x20, 0x31, 0x44, 0x24, 0x28, 0x21],
        [0x09, 0x30, 0x3e, 0x3f, 0x52, 0x28, 0x45, 0x13, 0x21],
        [0x24, 0x24, 0x38, 0xba, 0x54, 0x28, 0x45, 0x13, 0x21],
    ];

    private static readonly byte[] CheatKey = "HELP ME!!"u8.ToArray();

    /// <summary>The cheat's code as the game compares it: the XOR, up to its terminating zero.</summary>
    public static byte[] CheatCode(int cheat)
    {
        List<byte> code = [];
        for (int i = 0; i < CheatKey.Length; i++)
        {
            byte b = (byte)((i < CheatNames[cheat].Length ? CheatNames[cheat][i] : 0) ^ CheatKey[i]);
            if (b == 0) break;
            code.Add(b);
        }
        return [.. code];
    }

    /// <summary>
    /// <c>CFEPSaveGame::IsCheatActive</c> (0x00465490): every cheat with all cheats enabled; otherwise the save's name
    /// contains the code, case-sensitively, after <c>FromWCHAR</c> keeps each character's low byte (ending at a zero).
    /// </summary>
    public bool IsCheatActive(int cheat) => CheatActive(frontEnd.SaveGameName, frontEnd.AllCheatsEnabled, cheat);

    /// <summary><see cref="IsCheatActive"/> for a save name and the all-cheats flag.</summary>
    public static bool CheatActive(string saveName, bool allCheatsEnabled, int cheat)
    {
        if (allCheatsEnabled) return true;
        List<byte> name = [];
        foreach (char c in saveName)
        {
            byte b = (byte)c;
            if (b == 0) break;
            name.Add(b);
        }
        return name.ToArray().AsSpan().IndexOf(CheatCode(cheat)) >= 0;
    }
}
