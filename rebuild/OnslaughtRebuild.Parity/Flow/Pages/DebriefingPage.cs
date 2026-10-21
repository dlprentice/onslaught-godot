// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FEPDebriefing.cpp (reconstructed from the retail program; not in Stuart's release):
// RenderPreCommon 00456d40 and Render 00456dd0, and src/FrontEndText.cpp (CFrontEndText::GetLevelName 00469550).

using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Particles;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The debriefing (<c>CFEPDebriefing</c>): the level's result, then each new goodie flies across the screen, one every
/// half second. Once the last goodie has crossed the middle, the page fades in its prompt, and select, back or a click
/// anywhere returns to the level select. The first goodie ever won also explains goodies in a message box.
/// </summary>
/// <remarks>
/// The existing shared particle simulation owns effect creation and its CRT draws. Not modelled: the
/// auto-configuration test's presses and box timeout. The goodies' particles are drawn (or not) by the front end's
/// particle render, not by this page.
/// </remarks>
public sealed class DebriefingPage(FrontEnd frontEnd) : FrontEndPageBase
{
    public const int MaxGoodies = 100;

    private FVector[]? _positions;
    private ParticleEffectLink[]? _links;

    public float FastUntil { get; private set; }
    public int NumShown { get; private set; }
    public float NextTime { get; private set; }
    public int NumGoodies { get; private set; }
    public bool FirstGoodie { get; private set; }
    public float Fade { get; private set; }
    public float StartTime { get; private set; }

    /// <summary>The goodie's position along the screen (<c>mPositions[i].X</c>), which starts at -3 when it is shown.</summary>
    public float PositionX(int goodie) => Position(goodie).X;

    public FVector Position(int goodie) => _positions![goodie];
    public ParticleEffectLink? Link(int goodie) => _links?[goodie];

    /// <summary>
    /// What <c>mPositions</c> holds before a goodie is shown. <c>FVector</c>'s constructor leaves it uninitialised, so
    /// retail's value is whatever the game's heap last held there: an open question, answered for a run by when its
    /// prompt appears with two or more goodies (the last goodie's X, moving 0.1 a frame, starts the fade once above 0).
    /// Zero by default.
    /// </summary>
    public float UnshownPositionX { get; set; }

    /// <summary><c>CFEPDebriefing::Init</c> (0x00456780).</summary>
    public override bool Init()
    {
        _positions = new FVector[MaxGoodies];
        Array.Fill(_positions, new FVector(UnshownPositionX, 0, 0));
        _links = frontEnd.Particles is { } particles
            ? Enumerable.Range(0, MaxGoodies).Select(_ => new ParticleEffectLink(particles.Manager)).ToArray()
            : null;
        FastUntil = 0f;
        NumShown = 0;
        NextTime = 0f;
        return true;
    }

    /// <summary><c>CFEPDebriefing::Shutdown</c> (0x00456850).</summary>
    public override void Shutdown()
    {
        if (_links is not null) foreach (ParticleEffectLink link in _links) link.Release();
        _links = null;
        _positions = null;
    }

    /// <summary><c>CFEPDebriefing::ButtonPressed</c> (0x004568a0): select or back once the prompt is half in; the cheat button hurries the effect.</summary>
    public override void ButtonPressed(int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuSelect:
            case FrontEndButtons.MenuBack:
                if (Fade > 0.5f)
                {
                    frontEnd.SetPage(FrontEndPage.LevelSelect, 30);
                    frontEnd.PlaySound(FrontEndSound.Select);
                    if (_links is not null) foreach (ParticleEffectLink link in _links) link.Stop(false);
                }
                break;
            case FrontEndButtons.Cheat:
                FastUntil = (float)X87.Add(frontEnd.Clock.GetSysTimeFloat(), 0.1f);
                break;
        }
    }

    /// <summary>
    /// <c>CFEPDebriefing::Process</c> (0x00456930): while active and no box is up, every goodie moves 0.1 along; the first
    /// goodie ever explains itself once it is shown and has reached 0.45; a new goodie appears every half second (the
    /// faster effect while hurried). The prompt starts fading in once the last goodie is past 0, at 0.02 a frame.
    /// </summary>
    public override void Process(FrontEndPageState state)
    {
        FVector[] positions = _positions!;
        FrontEndMessageBox box = frontEnd.MessageBox;
        if (box.BeingDisplayed)
        {
            // The auto-configuration test's box timeout (not modelled).
        }
        else if (state == FrontEndPageState.Active)
        {
            for (int i = 0; i < NumGoodies; i++)
            {
                positions[i].X = (float)X87.Add(positions[i].X, 0.1f);
                _links?[i].SetPos(positions[i]);
            }
            if (FirstGoodie && NumShown > 0 && positions[0].X >= 0.45f)
            {
                FirstGoodie = false;
                // TEXT id 0x37416e9: the first goodie's explanation.
                frontEnd.ShowMessage(320f, 270f, 350f, t => t.ById(0x37416e9), 0, 10, MessageBoxOption.Ok, 0, 0.01f);
                box.SetOnDismiss(FrontEndPage.None, 0);
            }
            bool fast = frontEnd.Clock.GetSysTimeFloat() < FastUntil;
            if (NumShown < NumGoodies && frontEnd.Clock.GetSysTimeFloat() > NextTime)
            {
                ParticleManager manager = frontEnd.Particles?.Manager ?? throw new InvalidOperationException(
                    "a new goodie needs the front-end particle resources");
                positions[NumShown] = new FVector(-3f, 0f, 1f);
                manager.CreateEffect(manager.Set.GetPD(fast ? "Goodie Effect 2" : "Goodie Effect"),
                    _links![NumShown], default, false, false);
                _links[NumShown].SetPos(positions[NumShown]);
                NumShown++;
                NextTime = (float)X87.Add(frontEnd.Clock.GetSysTimeFloat(), 0.5f);
            }
        }

        // With no goodies the fade has normally already started, so the last position is never read. While the page is
        // inactive before its first transition (no goodies, no fade) retail reads mPositions[-1], the memory manager's
        // word before the array: whatever it does to the fade, TransitionNotification sets the fade again before the
        // page shows, so the core reads nothing there.
        if (Fade == 0f && NumGoodies > 0 && positions[NumGoodies - 1].X > 0f) Fade = 0.001f;
        if (Fade != 0f)
        {
            Fade = (float)X87.Add(Fade, 0.02f);
            if (Fade > 1f) Fade = 1f;
        }
    }

    /// <summary>
    /// <c>CFEPDebriefing::RenderPreCommon</c> (0x00456d40): sets FED's camera before either page's ordinary render, then
    /// draws the standard video background.
    /// </summary>
    public override void RenderPreCommon(float transition, FrontEndPage dest)
    {
        if (frontEnd.Particles is { } particles) particles.Camera = (new FVector(0, -2, 0), FMatrix.Identity);
        frontEnd.DrawStandardVideoBackground(transition, 0x3fffffff, dest);
    }

    /// <summary>
    /// <c>CFEPDebriefing::Render</c> (0x00456dd0). Its state-changing part: the help arrow (select, sliding with the
    /// prompt's fade unless leaving for the level select), then, once settled and with no interactive box, a click
    /// anywhere selects. With <see cref="FrontEnd.Draw"/> it also draws: the level select's ring (shrinking back as the
    /// page arrives), the Forseti writing and small logo once settled, the level's name, the mission status and the
    /// objectives' results sliding in from the left, a won level's grade (its brackets closing in, then the letter
    /// landing with a flash and glinting every four seconds), and the title bar.
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        FrontEndDraw? draw = frontEnd.Draw;
        if (draw is not null)
        {
            if (transition == 1f)
            {
                frontEnd.RenderSmallForsetiText(1f);
                draw.DrawColouredSprite(285f, 225f, 0.9f, FrontEndTexture.MetalRingTransFromLevsel2, 0xffffffff, 1.6f, 1.6f, SpriteAlign.Centre);
            }
            else if (dest != FrontEndPage.LevelSelect)
            {
                float r = transition * -1f;
                float sc = (r + 2f) * 1.6f;
                draw.DrawColouredSprite(285f, 225f, 0.9f, FrontEndTexture.MetalRingTransFromLevsel2, 0xffffffff, sc, sc, SpriteAlign.Centre,
                    SpriteType.None, 1f, r + 1f);
            }
            if (transition == 1f) frontEnd.RenderSmallBEALogo(1f);
        }

        frontEnd.RenderHelpArrow(4, dest == FrontEndPage.LevelSelect ? transition : Fade);

        if (draw is not null) DrawResults(draw, transition, dest);

        if (transition == 1f && !frontEnd.MessageBox.IsInteractive)
            frontEnd.ProcessMouseClickInRect(0f, 0f, 640f, 480f, FrontEndButtons.MenuSelect);

        // FET.GetText(FEX_DEBRIEFING), read by its id (see DrawResults).
        if (draw is not null && frontEnd.Texts is { } texts) frontEnd.DrawTitleBar(texts.ById(texts.Ids["FETX_DEBRIEFING"]), transition, dest);
    }

    /// <summary>
    /// The results: the level's name (normal font), the mission status and the primary and secondary objectives (small
    /// font, each label followed by its result in red, green or grey), then a won level's grade. Retail reads the labels
    /// and results with <c>FET.GetText</c> and the grade's clock with <c>PLATFORM.GetSysTimeFloat</c>; drawing must not
    /// change the simulation, so the texts are read by their ids (as GetText does with the English fallback off) and the
    /// clock is the one the frame's render started with.
    /// </summary>
    private void DrawResults(FrontEndDraw draw, float transition, FrontEndPage dest)
    {
        if (frontEnd.NormalFont is not { } normal || frontEnd.SmallFont is not { } small || frontEnd.Texts is not { } texts) return;
        var normalKind = CanvasTextureKind.NormalFont;
        var smallKind = CanvasTextureKind.SmallFont;
        string Fet(string name) => texts.ById(name == "FETX_PRIMARY_OBJECTIVES" ? PrimaryObjectivesId : texts.Ids[name]);
        Career.EndLevelData end = frontEnd.EndLevelData;
        // GAME_STATE_LEVEL_LOST and GAME_STATE_LEVEL_WON.
        const int lost = 4, won = 5;

        float tr = FrontEndDraw.RangeTransition(transition, 0.75f, 1f);
        float d = 1f - tr;
        float off = d * d * -800f;
        int alpha = X87.Fist(tr * 255f);
        uint alphaBits = unchecked((uint)alpha << 24);
        draw.DrawTextDynamic(normal, normalKind, off + 130f, 149f, 0.1f, 1f, 1f, alphaBits | 0xffffff, LevelName(texts, end.WorldFinished), 1000f);

        int maxw = -1;
        maxw = Math.Max(maxw, small.GetTextExtent(Fet("FETX_MISSION_STATUS")).Width);
        maxw = Math.Max(maxw, small.GetTextExtent(Fet("FETX_PRIMARY_OBJECTIVES")).Width);
        maxw = Math.Max(maxw, small.GetTextExtent(Fet("FETX_SECONDARY_OBJECTIVES")).Width);
        float tab = maxw + 20f;

        // TEXT.GetLanguage(): the front end's text database is the English one (language 0), so the separator is ": ".
        const string separator = ": ";
        uint lcol = alphaBits | 0xffaf3f;
        draw.DrawTextDynamic(small, smallKind, off + 130f, 184f, 0.1f, 1f, 1f, lcol, Fet("FETX_MISSION_STATUS") + separator, 1000f);

        (string text, uint col) = end.FinalGameState switch
        {
            lost => (Fet("FETX_DEFEAT"), 0xffff3f1fu),
            won => (Fet("FETX_VICTORY"), 0xff3fff2fu),
            _ => (Fet("FETX_ABORTED"), 0xff3f3f3fu),
        };
        draw.DrawTextDynamic(small, smallKind, tab + off + 130f, 184f, 0.1f, 1f, 1f, (col & 0xffffff) | alphaBits, text, 1000f);

        float y = 210f;
        int primary = Summary(end.PrimaryObjectives);
        if (end.WorldFinished == 500 && end.FinalGameState == won) primary = 1;
        if (primary != -1)
        {
            draw.DrawTextDynamic(small, smallKind, off + 130f, y, 0.1f, 1f, 1f, lcol, Fet("FETX_PRIMARY_OBJECTIVES") + separator, 1000f);
            (text, col) = primary == 1 ? (Fet("FETX_COMPLETE"), 0xff3fff2fu) : (Fet("FETX_INCOMPLETE"), 0xffff3f1fu);
            draw.DrawTextDynamic(small, smallKind, tab + off + 130f, y, 0.1f, 1f, 1f, (col & 0xffffff) | alphaBits, text, 1000f);
            y += 16f;
        }

        int secondary = Summary(end.SecondaryObjectives);
        if (secondary != -1)
        {
            draw.DrawTextDynamic(small, smallKind, off + 130f, y, 0.1f, 1f, 1f, lcol, Fet("FETX_SECONDARY_OBJECTIVES") + separator, 1000f);
            (text, col) = secondary == 1 ? (Fet("FETX_COMPLETE"), 0xff3fff2fu) : (Fet("FETX_INCOMPLETE"), 0xffff3f1fu);
            draw.DrawTextDynamic(small, smallKind, tab + off + 130f, y, 0.1f, 1f, 1f, (col & 0xffffff) | alphaBits, text, 1000f);
        }

        if (end.FinalGameState != won) return;
        int a = DrawGradeBrackets(draw, transition, dest);
        draw.DrawTextDynamic(normal, normalKind, off + 130f, 295f, 0.1f, 1f, 1f, unchecked((uint)a << 24) | 0xffffff, Fet("FETX_GRADE"), 1000f);
        DrawGrade(draw, transition, end.Ranking);
    }

    /// <summary>
    /// FETX_PRIMARY_OBJECTIVES: text.stf defines it as -846478193, and <see cref="Text.TextIds"/> reads only unsigned ids,
    /// so it is given here as the 32-bit id the game compiles it to.
    /// </summary>
    private const uint PrimaryObjectivesId = unchecked((uint)-846478193);

    // -1 when no objective of the kind is defined, 1 when all defined are complete, 0 otherwise.
    private static int Summary(Career.ObjectiveState[] objectives)
    {
        int summary = -1;
        foreach (Career.ObjectiveState state in objectives)
        {
            if (state == Career.ObjectiveState.NotDefined) continue;
            if (state == Career.ObjectiveState.Complete)
            {
                if (summary == -1) summary = (int)state;
            }
            else summary = 0;
        }
        return summary;
    }

    /// <summary>
    /// A won level's grade brackets: the inner closing in as the page arrives (and flying apart towards the level select),
    /// the outer shown only while it arrives. Returns the alpha the grade's label is drawn with (the outer bracket's).
    /// </summary>
    private int DrawGradeBrackets(FrontEndDraw draw, float transition, FrontEndPage dest)
    {
        int a = 255;
        float scale = 1.25f, ang = 0f;
        if (transition < 1f)
        {
            if (dest != FrontEndPage.LevelSelect)
            {
                if (transition < 0.1f) a = 0;
                else if (transition < 0.3f)
                {
                    float part = FrontEndDraw.RangeTransition(transition, 0.1f, 0.3f);
                    a = FrontEndDraw.MakeAlpha(part);
                    scale = part;
                    ang = -(part * 0.3f);
                }
                else if (transition < 0.5f) scale = 1f;
                else if (transition < 0.7f) scale = FrontEndDraw.RangeTransition(transition, 0.5f, 0.7f) * 0.25f + 1f;
            }
            else
            {
                float c = 1f - FrontEndDraw.RangeTransition(transition, 0.75f, 1f);
                a = FrontEndDraw.MakeAlpha(1f - c);
                scale = c * 1.4f + 1.25f;
                ang = -c;
            }
        }

        float shx = FrontEndDraw.ShadowOffsetX(frontEnd.Counter), shy = FrontEndDraw.ShadowOffsetY(frontEnd.Counter);
        draw.DrawColouredSprite(shx + 325f, shy + 320f, 0.35f, FrontEndTexture.BeaTitleSymbolBracket01, FrontEndDraw.BlendAlpha2(0x3f000000, a),
            scale * 1.05f, scale * 1.05f, SpriteAlign.Centre, SpriteType.None, 1f, ang);
        draw.DrawColouredSprite(320f, 310f, 0.3f, FrontEndTexture.BeaTitleSymbolBracket01, FrontEndDraw.BlendAlpha2(0xffffffff, a),
            scale, scale, SpriteAlign.Centre, SpriteType.None, 1f, ang);

        a = 255;
        ang = 0f;
        if (transition < 1f && dest != FrontEndPage.LevelSelect && !(transition < 0.1f))
        {
            bool show = true;
            if (transition < 0.3f)
            {
                float part = FrontEndDraw.RangeTransition(transition, 0.1f, 0.3f);
                a = FrontEndDraw.MakeAlpha(part);
                scale = part;
                ang = -(part * 0.3f);
            }
            else if (transition < 0.7f) scale = 1f;
            else if (transition < 0.9f)
            {
                float part = 1f - FrontEndDraw.RangeTransition(transition, 0.7f, 0.9f);
                a = FrontEndDraw.MakeAlpha(part);
                scale = part;
                ang = -(part * 0.3f);
            }
            else show = false;

            if (show)
            {
                draw.DrawColouredSprite(shx + 325f, shy + 320f, 0.35f, FrontEndTexture.BeaTitleSymbolBracket02, FrontEndDraw.BlendAlpha2(0x3f000000, a),
                    scale * 1.05f, scale * 1.05f, SpriteAlign.Centre, SpriteType.None, 1f, ang);
                draw.DrawColouredSprite(320f, 310f, 0.3f, FrontEndTexture.BeaTitleSymbolBracket02, FrontEndDraw.BlendAlpha2(0xffffffff, a),
                    scale, scale, SpriteAlign.Centre, SpriteType.None, 1f, ang);
            }
        }
        return a;
    }

    /// <summary>
    /// The grade's letter (its textures clamped at their edges): a second after the page opens it zooms down to its size
    /// with its shadow, drawn over everything; once landed it glints (the scrolling glare, added) for one second in every
    /// four, and while landing a larger copy flashes over it.
    /// </summary>
    private void DrawGrade(FrontEndDraw draw, float transition, float ranking)
    {
        sbyte grade = Career.Career.GradeFromRanking(ranking);
        FrontEndTexture gtex = grade == 'S' ? FrontEndTexture.RankingS : FrontEndTexture.RankingA + (grade - 'A');
        // A won level's ranking is from 0 to 1 (S, A to E); any other grade would index past the ranking textures.
        if (gtex is < FrontEndTexture.RankingS or > FrontEndTexture.RankingE) return;

        // PLATFORM.GetSysTimeFloat(), as the frame's render read it.
        float t = frontEnd.LastRenderTime - StartTime;
        if (!(t > 1f) || !(transition > 0.75f)) return;
        float s = (0.5f - t + 1f) * 10f;
        float flash = -1f;
        if (s < 1f)
        {
            flash = 1f - s;
            s = 1f;
        }

        int ga = X87.Fist(1f / s * 255f);
        draw.DrawColouredSprite(323f, 313f, 0.05f, gtex, FrontEndDraw.BlendAlpha2(0x3f000000, ga), s, s, SpriteAlign.Centre, clamp: true);
        draw.ZAlways = true;
        draw.DrawColouredSprite(320f, 310f, 0.999f, gtex, FrontEndDraw.BlendAlpha2(0xffffffff, ga), s, s, SpriteAlign.Centre, clamp: true);
        draw.ZAlways = false;

        bool clamp = true;
        if (s == 1f && t % 4f < 1f)
        {
            // Retail also turns the alpha test on here and leaves it on; the canvas has no alpha test.
            draw.Blend = CanvasBlend.Additive;
            clamp = false;
            draw.DrawColouredSprite(320f, 310f, 0.998f, FrontEndTexture.GlareAlpha, 0xffffffff, 1f, 1f, SpriteAlign.Centre, SpriteType.ScrollV, t);
            draw.Blend = CanvasBlend.Modulate;
        }

        if (flash < 3f && flash > 0f)
        {
            float f2 = flash * 2f;
            int fa = X87.Fist(1f / (f2 * 2f) * 128f);
            draw.DrawColouredSprite(320f, 310f, 0.02f, gtex, FrontEndDraw.BlendAlpha2(0xffffffff, fa), f2, f2, SpriteAlign.Centre, clamp: clamp);
        }
    }

    /// <summary><c>CFrontEndText::GetLevelName</c> (0x00469550): the level's name from the language file (200 and 201 share one).</summary>
    private static string LevelName(Text.FrontEndTexts texts, int level) => level switch
    {
        200 or 201 => texts.ById(texts.Ids["FETX_LEVEL_NAME_200"]),
        100 or 110 or 211 or 212 or 221 or 222 or 231 or 232 or 300 or 311 or 312 or 321 or 322 or 331 or 332 or 400 or 411
            or 412 or 421 or 422 or 431 or 432 or 500 or 511 or 512 or 521 or 522 or 523 or 524 or 600 or 611 or 612 or 621
            or 622 or 700 or 710 or 720 or 731 or 732 or 741 or 742 or 800 => texts.ById(texts.Ids[$"FETX_LEVEL_NAME_{level}"]),
        _ => "Unnamed Level",
    };

    /// <summary>
    /// <c>CFEPDebriefing::TransitionNotification</c> (0x00457cf0): the career counts its goodies and hands over how many
    /// are new (at most 99) and whether the first ever was among them; with none the prompt starts fading in at once.
    /// </summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        OnslaughtRebuild.Parity.Career.Career career = frontEnd.Career;
        career.CountGoodies();
        NumGoodies = career.GetAndResetGoodieNewCount();
        FirstGoodie = career.GetAndResetFirstGoodie();
        if (NumGoodies >= MaxGoodies) NumGoodies = MaxGoodies - 1;
        Fade = NumGoodies > 0 ? 0f : 0.001f;
        StartTime = (float)X87.Add(frontEnd.Clock.GetSysTimeFloat(), 0.2f);
    }
}
