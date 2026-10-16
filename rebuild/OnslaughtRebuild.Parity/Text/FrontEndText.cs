// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FrontEndText.cpp (CFrontEndText::GetText; reconstructed from the retail program, not in
// Stuart's release).

using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Text;

/// <summary>The front-end texts the ported pages measure (<c>EFETextHack</c>), with their ids' names in text.stf.</summary>
public enum FrontEndText
{
    NewGame,
    ContinueGame,
    LoadGame,
    SaveGame,
    Multiplayer,
    Goodies,
    Options,
    Language,
    Quit,
    Ok,
    Yes,
    No,
    AbandonCareer,
    NoAutoSave,
    OverwriteSaveGame,
    ConfirmSaveGameDelete,
    SaveGameCouldNotDelete,
    LoadInvalid,
    BadSaveVersion,
    SaveError,

    /// <summary>The PC's "too many saves" and "no space" texts, which return the PC text 0xb5 (<c>PCText</c>).</summary>
    TooManySaves,
}

/// <summary>
/// <c>CFrontEndText::GetText</c> (0x0046a2a0, <c>FET.GetText</c>): a front-end text in the loaded language. Every call
/// first reads the X key once and toggles the English fallback (<c>sShowEnglish</c>), which the retail program's own
/// English strings answer. The program's own strings (the English fallback, and <c>Localization_GetStringById</c>'s PC
/// texts) come from the host, from the player's own executable: they are never carried in this source.
/// </summary>
public sealed class FrontEndTexts(TextDatabase database, TextIds ids, PlatformInput platform,
    Func<int, string>? pcText = null, Func<FrontEndText, string>? englishFallback = null)
{
    /// <summary>text.stf's names.</summary>
    public TextIds Ids => ids;

    /// <summary><c>Localization_GetStringById</c> (0x00524830): a PC text, from the host.</summary>
    public string PcText(int id) =>
        (pcText ?? throw new NotSupportedException("The PC texts come from the player's executable; the host must supply them."))(id);

    /// <summary>A string of the language file by its id (<c>TEXT.GetStringById</c>).</summary>
    public string ById(uint id) => database.GetStringById(id);

    /// <summary><c>sShowEnglish</c> (00679b88).</summary>
    public bool ShowEnglish { get; private set; }

    public string GetText(FrontEndText text)
    {
        if (platform.KeyOnce(Dik.X)) ShowEnglish = !ShowEnglish;
        if (ShowEnglish)
            return (englishFallback ?? throw new NotSupportedException(
                "The English fallback (GetAsciiText) comes from the player's executable; the host must supply it."))(text);
        if (text == FrontEndText.TooManySaves)
        {
            database.GetStringById(ids["FETX_TOOMANYSAVES_PS2"]);
            return PcText(0xb5);
        }
        return database.GetStringById(ids[text switch
        {
            FrontEndText.NewGame => "FETX_NEW_GAME",
            FrontEndText.ContinueGame => "FETX_CONTINUE_GAME",
            FrontEndText.LoadGame => "FETX_LOAD_GAME",
            FrontEndText.SaveGame => "FETX_SAVE_GAME",
            FrontEndText.Multiplayer => "FETX_MULTIPLAYER",
            FrontEndText.Goodies => "FETX_GOODIES",
            FrontEndText.Options => "FETX_OPTIONS",
            FrontEndText.Language => "FETX_LANGUAGE",
            FrontEndText.Ok => "FETX_OK",
            FrontEndText.Yes => "FETX_YES",
            FrontEndText.No => "FETX_NO",
            FrontEndText.AbandonCareer => "FETX_ABANDONCG",
            FrontEndText.NoAutoSave => "FETX_NOAUTOSAVE",
            FrontEndText.OverwriteSaveGame => "FETX_OVERWRITE_SAVEGAME",
            FrontEndText.ConfirmSaveGameDelete => "FETX_CONFIRM_SAVEGAME_DELETE",
            FrontEndText.SaveGameCouldNotDelete => "FETX_SAVEGAME_COULD_NOT_DELETE",
            FrontEndText.LoadInvalid => "FETX_LOADINVALID_XBOX",
            FrontEndText.BadSaveVersion => "FETX_BADSAVEVERSION_XBOX",
            FrontEndText.SaveError => "FETX_SAVEERROR",
            _ => "FETX_QUIT",
        }]);
    }
}
