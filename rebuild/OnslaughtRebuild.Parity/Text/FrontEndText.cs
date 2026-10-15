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
}

/// <summary>
/// <c>CFrontEndText::GetText</c> (0x0046a2a0, <c>FET.GetText</c>): a front-end text in the loaded language. Every call
/// first reads the X key once and toggles the English fallback (<c>sShowEnglish</c>), which the retail program's own
/// English strings answer.
/// </summary>
public sealed class FrontEndTexts(TextDatabase database, TextIds ids, PlatformInput platform)
{
    /// <summary><c>sShowEnglish</c> (00679b88).</summary>
    public bool ShowEnglish { get; private set; }

    public string GetText(FrontEndText text)
    {
        if (platform.KeyOnce(Dik.X)) ShowEnglish = !ShowEnglish;
        if (ShowEnglish)
            throw new NotSupportedException("The English fallback (GetAsciiText, the program's own strings) is not ported.");
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
            _ => "FETX_QUIT",
        }]);
    }
}
