// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/MessageBox.cpp and MessageBox.h (CMessage 004b6e50, ~CMessage 004b7160,
// SampleFinishedPlaying 004b71d0; CMessageBox 004b71e0, ~CMessageBox 004b7930, SwitchMessagesOn 004b7b60,
// SwitchMessagesOff 004b7b70, TryAdvance 004b7b80, AddMessage 004b7ca0, PlayPendingSamples 004b7d90, StartMessage
// 004b7ea0, AdvanceReveal 004b8020, HandleEvent 004b81d0, StopCurrentPlayingSound 004b8800, InitResources 004b7320,
// GetPortrait 004b7ab0, RenderBattleLinePulse 004b82b0, RenderOverlay 004b8850), src/Hud.cpp (CHud::RenderObjectiveBar
// 00482210, the message tweaks) and src/SPtrSet.cpp (GenericSPtrSet::operator= 004e58a0), reconstructed from the retail
// executable; not in Stuart's release.

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Script;

/// <summary>TEXT (CText) as the message box and the message natives read it.</summary>
public interface IGameText
{
    /// <summary>CText::GetStringById: the localized text (UTF-16, as WCHAR), or null.</summary>
    string? GetString(uint id);

    /// <summary>CText::GetSoundName: the id's spoken sample, or null.</summary>
    string? GetSoundName(uint id);

    /// <summary>CText::GetLanguageName: "english", "french", ...</summary>
    string LanguageName { get; }
}

/// <summary>
/// The voice side of the message box: the async voice loader (gVoiceLoader), SOUND's sample and its play. Whoever models
/// the sound system calls <see cref="ScriptMessage.SampleFinishedPlaying"/> when a playing voice ends (CSoundManager's
/// finished callback), which is what lets a message finish before its 12-second cap.
/// </summary>
public interface IMessageVoice
{
    /// <summary>SOUND.IsInitialised().</summary>
    bool SoundInitialised { get; }

    /// <summary>gVoiceLoader.Acquire, the file name and StartAsync: the voice file starts loading.</summary>
    void StartLoad(string path);

    /// <summary>gVoiceLoader.IsBusy().</summary>
    bool LoaderBusy { get; }

    /// <summary>gVoiceLoader.mSize != 0: the load produced data (zero means it failed or nothing is pending).</summary>
    bool HasLoadedData { get; }

    /// <summary>
    /// The old sample is deleted, SOUND.CreateSampleFromData from the loaded data (the loader's size goes to zero), and
    /// SOUND.PlaySample on it with the message as the audible thing (null when the message has gone).
    /// </summary>
    void PlayLoaded(ScriptMessage? message);

    /// <summary>KILL_VOICE: every instance of the playing voice sample is killed and the sample deleted.</summary>
    void KillVoice();

    /// <summary>
    /// SOUND.UpdateStatus's part for the voice (CGame's AfterUpdate, each main loop): a voice whose buffer has stopped
    /// is stopped as a sound event, which tells its message (StopSoundEvent: SampleFinishedPlaying).
    /// </summary>
    void UpdateStatus();
}

/// <summary>CMessage: a radio message queued in the box (an IAudibleThing, so readers watch it).</summary>
public sealed class ScriptMessage : Monitor
{
    /// <summary>1/22 as retail stores it (0x3d3a2e8c at 005dc6ac); the duration multiplies by it.</summary>
    private const float OneOver22 = 0.045454547f;

    /// <summary>
    /// CMessage::CMessage: mDuration = len * (1/22) + 0.2, each operation rounded to 24 bits (fild of the length as a
    /// qword, fmul, fadd; 004b6edd..004b6efd).
    /// </summary>
    public ScriptMessage(string? name, string text, int stringId, float delay, Thing? speaker, ScheduledEvent? doneEvent,
        int priority)
    {
        FinishedPlaying = false;
        Text = text;
        StringId = stringId;
        Name = name;
        Delay = delay;
        Event = doneEvent;
        Priority = priority;
        HasSpeaker = false;
        if (speaker is not null)
        {
            Speaker.SetReader(speaker);
            HasSpeaker = true;
        }

        Duration = (float)X87.Add(X87.Mul(text.Length, OneOver22), 0.2f);
        Revealed = 0;
        Dots = 0;
    }

    /// <summary>mFinishedPlaying: its voice has played (or it has none).</summary>
    public bool FinishedPlaying { get; set; }

    /// <summary>mText (wcslen is its UTF-16 length).</summary>
    public string Text { get; }

    public int StringId { get; }

    /// <summary>mDuration: the text's length / 22 + 0.2 seconds.</summary>
    public float Duration { get; }

    /// <summary>mName: the speaker's name.</summary>
    public string? Name { get; }

    /// <summary>mRevealed: letters shown so far.</summary>
    public int Revealed { get; private set; }

    public float Delay { get; }

    /// <summary>mEndTime (display only).</summary>
    public float EndTime { get; set; }

    /// <summary>mEvent: the free event posted when the message has been shown (a Wait native's resume).</summary>
    public ScheduledEvent? Event { get; set; }

    /// <summary>mPriority: the queue is sorted on it.</summary>
    public int Priority { get; }

    public ActiveReader<Thing> Speaker { get; } = new();

    /// <summary>mDots: the waiting dots after the text of a speaker who has gone.</summary>
    public int Dots { get; set; }

    public bool HasSpeaker { get; }

    /// <summary>
    /// CMessage::WordWrap (004b6f70): the revealed text (and its waiting dots) in lines of fewer than
    /// <paramref name="width"/> characters, a word kept whole (a space before !, ?, ; or : does not break), at most
    /// <paramref name="maxLines"/> lines (the earliest scroll away). Returns the eight lines, empty ones as "".
    /// </summary>
    public string[] WordWrap(int width, int maxLines)
    {
        const int Stride = 0x56;
        var lines = new char[8 * Stride];
        string text = Text;
        int total = text.Length + Dots;
        int revealed = Revealed;
        int end = revealed + Dots;
        int col = 0;
        int n = 0;
        char At(int i) => i < text.Length ? text[i] : '\0';
        if (end > 0)
        {
            int pos = 0;
            for (int i = 0; i < end; i++)
            {
                int len = 0;
                if (i < revealed)
                {
                    int p = i;
                    while (true)
                    {
                        bool last = i + len >= total;
                        bool space = At(p) == ' ';
                        if (space && i + len + 1 < total)
                        {
                            char c = At(i + len + 1);
                            if (c is '!' or '?' or ';' or ':')
                            {
                                space = false;
                            }
                        }

                        if (space || last)
                        {
                            break;
                        }

                        len++;
                        p++;
                        if (Dots != 0 && i + len >= revealed)
                        {
                            len += Dots;
                            break;
                        }
                    }
                }
                else
                {
                    len = revealed - i + 5;
                }

                if (len + col >= width)
                {
                    lines[pos + col] = '\0';
                    col = 0;
                    n++;
                    pos += Stride;
                    if (n >= maxLines)
                    {
                        for (int k = 0; k < 7; k++)
                        {
                            for (int j = 0; j < 0x55; j++)
                            {
                                lines[(k * Stride) + j] = lines[((k + 1) * Stride) + j];
                            }
                        }

                        n--;
                        pos -= Stride;
                    }
                }

                char ch = At(i);
                if (ch == ' ' && col == 0)
                {
                    continue;
                }

                lines[pos + col++] = i < revealed ? ch : '.';
            }
        }

        lines[(n * Stride) + col] = '\0';
        var result = new string[8];
        for (int l = 0; l < 8; l++)
        {
            int start = l * Stride;
            int length = 0;
            while (length < Stride && lines[start + length] != '\0')
            {
                length++;
            }

            result[l] = new string(lines, start, length);
        }

        return result;
    }

    /// <summary>SampleFinishedPlaying (004b71d0): the sound system's callback.</summary>
    public void SampleFinishedPlaying() => FinishedPlaying = true;

    public void RevealLetter()
    {
        if (Revealed < Text.Length)
        {
            Revealed++;
        }
    }

    /// <summary>SpeakerGone: it had a speaker, who has gone or is dying.</summary>
    public bool SpeakerGone() => HasSpeaker && (Speaker.ToRead is null || Speaker.ToRead.IsDying);

    /// <summary>~CMessage (the event is not freed: it was posted or is lost), then the readers are told.</summary>
    public void Delete()
    {
        Event = null;
        Speaker.Release();
        Destroy();
    }
}

/// <summary>
/// CMessageBox: the in-game radio messages, one at a time in priority order. A message starts 0.2 s after it is taken,
/// reveals two letters a tick (every 0.038 s, so each game tick), and finishes once its text time has passed and its
/// voice has played (or 12 s have gone); 0.3 s later its event is posted and 0.2 s after that the next may start.
/// </summary>
public sealed class MessageBox : Monitor
{
    public const int Reveal = 3000;
    public const int Next = 3001;
    public const int Finish = 3002;

    /// <summary>ALLOWED_TO_PLAY_MESSAGES (CGame::StartPlayingState).</summary>
    public const int AllowedToPlayMessages = 3003;

    public const int Start = 3004;

    private readonly ThingContext _context;
    private readonly ActiveReader<ScriptMessage> _voiceMessage = new();
    private bool _deleted;

    /// <summary>CMessageBox::CMessageBox (CGame::InitRestartLoop makes it).</summary>
    public MessageBox(ThingContext context, IGameText text, IMessageVoice voice)
    {
        _context = context;
        Text = text;
        Voice = voice;
        Queue = new SPtrSet<ScriptMessage>(context.Pool);
        Current = null;
        AllowedToPlay = false;
        CanAdvance = true;
        VoiceName = null;
        PendingVoiceName = null;
        _voiceMessage.SetReader(null);
    }

    public IGameText Text { get; }

    public IMessageVoice Voice { get; }

    // --- The box's picture: state only its render reads (CMessageBox::RenderOverlay, RenderBattleLinePulse) -------------

    /// <summary>mUnknown020: the portrait's interference, 1 when a message starts or ends, easing to its delay.</summary>
    public float Interference { get; private set; }

    /// <summary>mUnknown024: the message is over (or its speaker gone): the portrait is not drawn.</summary>
    public bool PortraitOff { get; private set; }

    /// <summary>mRandom: the box's own stream (seed 123456), drawn only by its render.</summary>
    public Game.SeedUniformRandomNumberStream Random { get; } = new(123456);

    /// <summary>mPortrait: the speaker's portrait set (GetPortrait).</summary>
    public int Portrait { get; private set; }

    /// <summary>mUnknown1ac: the portrait's frame (its mouth).</summary>
    public int PortraitFrame { get; private set; }

    /// <summary>mPortraitTexture, as its name (null for a set without that frame).</summary>
    public string? PortraitTexture { get; private set; }

    /// <summary>mUnknown1b4: the event time of the frame's last change.</summary>
    public float PortraitTime { get; private set; }

    /// <summary>mOpen: the panel, 0 shut, 1 its frame open, 2 its text shown.</summary>
    public float Open { get; private set; }

    /// <summary>mUnknown1bc: the message's lines.</summary>
    public const int Lines = 3;

    /// <summary>mCurrent: the message being shown.</summary>
    public ScriptMessage? Current { get; private set; }

    /// <summary>mQueue.</summary>
    public SPtrSet<ScriptMessage> Queue { get; }

    /// <summary>mStartTime.</summary>
    public float StartTime { get; private set; }

    /// <summary>mAllowedToPlay.</summary>
    public bool AllowedToPlay { get; private set; }

    /// <summary>mCanAdvance (SwitchMessagesOn/Off).</summary>
    public bool CanAdvance { get; private set; }

    /// <summary>mVoiceName: the current message's voice, "language\messagebox\sample".</summary>
    public string? VoiceName { get; private set; }

    /// <summary>gVoiceName: the voice being loaded, null for none.</summary>
    public string? PendingVoiceName { get; private set; }

    /// <summary>The messages shown, in order (GAME.GetMessageLog()->AddMessage keeps them).</summary>
    public List<ScriptMessage> Shown { get; } = [];

    private EventManager Events => _context.Events;

    private GameState State => _context.Game?.State ?? GameState.Playing;

    public void SwitchMessagesOn()
    {
        CanAdvance = true;
        TryAdvance();
    }

    public void SwitchMessagesOff() => CanAdvance = false;

    /// <summary>TryAdvance: the first queued message starts in 0.2 s, if one may; a message whose speaker has gone is dropped.</summary>
    public void TryAdvance()
    {
        while (Queue.Size != 0 && Current is null && AllowedToPlay && CanAdvance && State <= GameState.Playing)
        {
            ScriptMessage m = Queue.First()!;
            if (m.SpeakerGone())
            {
                Queue.Remove(m);
                m.Delete();
                continue;
            }

            Current = m;
            StartTime = Events.Time;
            AllowedToPlay = false;
            Interference = 1f;
            VoiceName = null;
            PortraitOff = false;
            if (m.Speaker.ToRead is not null)
            {
                _context.Skip("CHud::SetMessageSpeaker");
            }

            Portrait = GetPortrait(m.Name ?? "");
            PortraitFrame = 0;
            PortraitTexture = PortraitTextures[Portrait][0];
            PortraitTime = Events.Time;
            Current.EndTime = (float)X87.Add(_context.Game?.LevelStartTime ?? 0.0f, Events.Time);
            Events.AddEventIn(0.2f, Start, this);
            return;
        }
    }

    /// <summary>
    /// AddMessage: into the queue before the first message of higher priority value (else last). Retail builds a local
    /// set and assigns it (operator= empties the queue and appends the local set's items), then the local set is freed.
    /// </summary>
    public void AddMessage(ScriptMessage message)
    {
        var sorted = new SPtrSet<ScriptMessage>(_context.Pool);
        bool added = false;
        for (ScriptMessage? m = Queue.First(); m is not null; m = Queue.Next())
        {
            if (message.Priority < m.Priority && !added)
            {
                sorted.Append(message);
                added = true;
            }

            sorted.Append(m);
        }

        if (!added)
        {
            sorted.Append(message);
        }

        Queue.RemoveAll();
        var it = new ListIterator<ScriptMessage>(sorted);
        for (ScriptMessage? m = it.First(); m is not null; m = it.Next())
        {
            Queue.Append(m);
        }

        sorted.RemoveAll();

        if (Queue.Size == 1 && AllowedToPlay)
        {
            TryAdvance();
        }
    }

    /// <summary>PlayPendingSamples (CGame::Update, each frame): a loaded voice starts playing.</summary>
    public void PlayPendingSamples()
    {
        if (_context.Game?.Paused == true || State > GameState.Playing || PendingVoiceName is null || Voice.LoaderBusy)
        {
            return;
        }

        if (!Voice.HasLoadedData)
        {
            _context.Skip("CMessageBox::PlayPendingSamples: no voice data (retail: FatalFileError if the read failed)");
            return;
        }

        Voice.PlayLoaded(_voiceMessage.ToRead);
        PendingVoiceName = null;
        _voiceMessage.SetReader(null);
    }

    /// <summary>StartMessage: the voice starts loading (or the message has none and counts as played), then the reveal.</summary>
    public void StartMessage()
    {
        ScriptMessage current = Current!;
        if (current.SpeakerGone())
        {
            Interference = 1f;
            PortraitOff = true;
            Events.AddEventIn(0.5f, Finish, this);
            return;
        }

        string? sound = Text.GetSoundName(unchecked((uint)current.StringId));
        if (sound is not null)
        {
            VoiceName = $"{Text.LanguageName}\\messagebox\\{sound}";
            if (Voice.SoundInitialised)
            {
                PendingVoiceName = VoiceName;
                _voiceMessage.SetReader(current);
                Voice.StartLoad($"data\\sounds\\{VoiceName}.ogg");
            }
        }
        else
        {
            ScriptRuntime.Of(_context).LogMessage($"ERROR:(MB) No sample for message '{current.Text}' id = {current.StringId}");
            current.FinishedPlaying = true;
        }

        AdvanceReveal();
    }

    /// <summary>
    /// AdvanceReveal: two more letters; again in 0.038 s while (duration + start) + 0.2 > now, or while the voice has not
    /// finished and now - start &lt;= 12; then the finish in 0.3 s. A gone speaker's message stops its voice and
    /// shows five dots before finishing in 2.7 s.
    /// </summary>
    public void AdvanceReveal()
    {
        ScriptMessage m = Current!;
        if (m.SpeakerGone())
        {
            Interference = 1f;
            if (m.Revealed < m.Text.Length)
            {
                StopCurrentPlayingSound();
                m.Dots++;
                if (m.Dots < 5)
                {
                    Events.AddEventIn(0.038f, Reveal, this);
                    return;
                }

                PortraitOff = true;
                Events.AddEventIn(2.7f, Finish, this);
                return;
            }

            PortraitOff = true;
        }

        m.RevealLetter();
        m.RevealLetter();

        float now = Events.Time;
        float textEnd = (float)X87.Add(X87.Add(m.Duration, StartTime), 0.2f);
        if (textEnd > now)
        {
            Events.AddEventIn(0.038f, Reveal, this);
            return;
        }

        if (!m.FinishedPlaying && (float)X87.Sub(now, StartTime) <= 12.0f)
        {
            Events.AddEventIn(0.038f, Reveal, this);
            return;
        }

        Interference = 1f;
        PortraitOff = true;
        Events.AddEventIn(0.3f, Finish, this);
    }

    /// <summary>StopCurrentPlayingSound (with CLIPARAMS.mUnknown18c clear): the voice is forgotten and killed.</summary>
    public void StopCurrentPlayingSound()
    {
        VoiceName = null;
        Voice.KillVoice();
    }

    /// <summary>
    /// ~CMessageBox004b7930: kill the global voice, delete queued messages, kill again, clear pending voice ownership,
    /// then notify readers. The process sound manager survives; stopped events remain deferred in its list.
    /// </summary>
    public void Delete()
    {
        if (_deleted) return;
        _deleted = true;
        Voice.KillVoice();
        foreach (ScriptMessage message in Queue.ToList()) message.Delete();
        Queue.RemoveAll();
        Current = null;
        AllowedToPlay = true;
        CanAdvance = true;
        Voice.KillVoice();
        VoiceName = null;
        PendingVoiceName = null;
        _voiceMessage.Release();
        Destroy();
    }

    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case Start:
                StartMessage();
                break;
            case Reveal:
                AdvanceReveal();
                break;
            case Finish:
            {
                ScriptMessage m = Current!;
                if (m.Event is not null)
                {
                    Events.AddEvent(m.Event);
                    m.Event = null;
                }

                Queue.Remove(m);
                Shown.Add(m);
                Current = null;
                Events.AddEventIn(0.2f, Next, this);
                break;
            }

            case Next:
            case AllowedToPlayMessages:
                AllowedToPlay = true;
                TryAdvance();
                break;
        }
    }

    // --- The box's picture ---------------------------------------------------------------------------------------

    /// <summary>InitResources' portraits by speaker (13 sets of up to 6 frames) and the speakers' name texts.</summary>
    private static readonly string?[][] PortraitTextures =
    [
        Set("Carver"), Set("tat"), Set("FC_Kramer"), [null, null, null, null, null, null], Set("fc_Tara"),
        [
            @"MessageBox\FC_Simmons_surprise_closed.tga", @"MessageBox\FC_Simmons_surprise_open.tga",
            @"MessageBox\FC_Simmons_stern_closed.tga", @"MessageBox\FC_Simmons_stern_open.tga",
            @"MessageBox\FC_Simmons_blank_closed.tga", @"MessageBox\FC_Simmons_blank_open.tga",
        ],
        Set("FC_Lorenzo"), Set("FC_Billy"), Set("cmdr"), Set("officer"), Set("rad_officr"), Set("technic"), Set("pilot"),
    ];

    private static readonly uint[] SpeakerIds =
        [0xdcdee, 0x170470, 0xe0831, 0x5c2de, 0x3c345, 0x1a19e0, 0x19f8cf, 0xcb1fd, 0x534dfa, 0x1837c4, 0x85595, 0xa13898, 0x552e30d];

    private static string?[] Set(string who) =>
        [$@"MessageBox\{who}_oo.tga", $@"MessageBox\{who}_ee.tga", $@"MessageBox\{who}_mm.tga", $@"MessageBox\{who}_aa.tga", null, null];

    /// <summary>
    /// GetPortrait (004b7ab0): the speaker whose name text equals the name (the last match), or the commander's set for
    /// a name containing text 0x0a336c2c (the first such); otherwise 2. Set 3 has no pictures and becomes 2.
    /// </summary>
    private int GetPortrait(string name)
    {
        bool found = false;
        int portrait = 2;
        for (int i = 0; i < SpeakerIds.Length; i++)
        {
            if (name == Text.GetString(SpeakerIds[i]))
            {
                portrait = i;
                found = true;
            }
            else if (!found && SpeakerIds[i] == 0x534dfa && Text.GetString(0x0a336c2c) is { } commander && name.Contains(commander, StringComparison.Ordinal))
            {
                portrait = i;
                found = true;
            }
        }

        return portrait == 3 ? 2 : portrait;
    }

    // The HUD in one 640 x 480 viewport (HUD.mViewport*, HUD_OFFSET_X/Y 0) and the message tweaks (Hud.cpp).
    private const float ViewportBottom = 480f, ViewportRight = 640f, ViewportWidth = 640f;
    private const float MessageBoxX = 10f, MessageBoxY = 23f, MessageBoxWidth = 22f, MessageTextY = 27f;
    private const int MessageWrapWidth = 26;

    /// <summary>
    /// RenderOverlay (004b8850), from CHud::RenderBattleline while a player shows the HUD: a finished game stops the
    /// voice; the panel opens (to 1, its frame) and widens (towards 2, its text) while a message shows, closes back to 1
    /// and then shuts when none is left; the objective bar is drawn at its opening and, once open, the message's lines.
    /// The opening and the voice stop happen whether or not anything is drawn.
    /// </summary>
    public void RenderOverlay(Flow.Draw.FrontEndDraw? draw, Text.BitmapFont? small)
    {
        Game.Game? game = _context.Game;
        if (game is not null && game.State > GameState.Playing)
        {
            StopCurrentPlayingSound();
            return;
        }

        float frameLength = _context.Particles?.RenderFrameLength ?? 1f;
        if (Current is null)
        {
            if (Open > 1f)
            {
                if (Queue.Size == 0)
                {
                    float step = frameLength * 0.5f;
                    bool opening = Open < 1f;
                    float dir = opening ? 0.01f : -0.01f;
                    Open += (Open - 2f + dir) * step;
                    if (opening)
                    {
                        if (Open > 1f) Open = 1f;
                    }
                    else if (Open < 1f)
                    {
                        Open = 1f;
                    }
                }
            }
            else
            {
                Open -= frameLength * 0.4f;
                if (Open < 0f) Open = 0f;
            }

            if (Open == 0f) return;
        }
        else if (Open < 1f)
        {
            Open += frameLength * 0.4f;
            if (Open > 1f) Open = 1f;
        }
        else
        {
            float f = frameLength * 0.5f;
            Open += (2f - Open) * f;
            if (MathF.Abs(2f - Open) < 0.01f) Open = 2f;
        }

        if (draw is null || small is null) return;
        float y = ViewportBottom + 16f - 128f + 48f + MessageBoxY;
        float width = 230f + MessageBoxWidth;
        float x = ViewportWidth * 0.5f + MessageBoxX + 12f;
        if (Open < 1f) ObjectiveBar(draw, x, y, width, 60f, Open);
        else ObjectiveBar(draw, x, y, (Open - 1f) * width, 60f, 1f);
        if (Current is not { } current || Open < 2f) return;
        string[] lines = current.WordWrap(MessageWrapWidth, Lines);
        float tx = x - width * 0.5f - 12f;
        var kind = new Flow.Draw.QuadTexture(Flow.Draw.CanvasTextureKind.SmallFont);
        draw.DrawTextWithShadow(small, kind, tx, y - MessageTextY, 0xffffffff, "  ", 0, 1f);
        for (int i = 0; i < 5; i++)
        {
            if (i < lines.Length) draw.DrawTextWithShadow(small, kind, tx, i * 15 + y - MessageTextY, 0xffffffff, lines[i], 0, 1f);
        }
    }

    /// <summary>CHud::RenderObjectiveBar (00482210): the panel's ends and middle at its width, or, half open, its ends turning in.</summary>
    private static void ObjectiveBar(Flow.Draw.FrontEndDraw draw, float x, float y, float width, float height, float open)
    {
        if (height < 10f) height = 10f;
        const string left = @"hud\v2\ObjectiveLeft.tga", right = @"hud\v2\ObjectiveRight.tga";
        const string innerLeft = @"hud\v2\ObjectiveInnerLeft.tga", innerRight = @"hud\v2\ObjectiveInnerRight.tga", innerCentre = @"hud\v2\ObjectiveInnerCentre.tga";
        const string any = Flow.Draw.FrontEndTextures.AnyFormat;
        if (open == 1f)
        {
            float scale = height * (1f / 64f);
            float half = width * 0.5f;
            float px = x - half;
            draw.DrawNamedSprite(left, px, y, 0.009f, 0xffffffff, scale, scale, Flow.Draw.SpriteAlign.Centre, format: any);
            draw.DrawNamedSprite(innerLeft, px, y, 0.009f, 0x90000000, scale, scale, (Flow.Draw.SpriteAlign)7, format: any);
            px = half + x;
            draw.DrawNamedSprite(right, px, y, 0.009f, 0xffffffff, scale, scale, Flow.Draw.SpriteAlign.Centre, format: any);
            draw.DrawNamedSprite(innerRight, px, y, 0.009f, 0x90000000, scale, scale, Flow.Draw.SpriteAlign.LeftMiddle, format: any);
            float segment = scale * 64f;
            float n = width / segment;
            px = x - half;
            while (n >= 1f)
            {
                draw.DrawNamedSprite(innerCentre, px, y, 0.009f, 0x90000000, scale, scale, Flow.Draw.SpriteAlign.LeftMiddle, format: any);
                n -= 1f;
                px += segment;
            }

            if (n > 0f)
                draw.DrawNamedSprite(innerCentre, px, y, 0.009f, 0x90000000, scale, scale, Flow.Draw.SpriteAlign.LeftMiddle,
                    Flow.Draw.SpriteType.FillRight, n, format: any);
        }
        else if (open > 0.5f && open < 1f)
        {
            float scale = height * open * (1f / 64f);
            int alpha = Math.Clamp((int)(open * 255f), 0, 255);
            uint col = unchecked((uint)(alpha * 255) << 16) | 0xffffff;
            float turn = 1f - open;
            draw.DrawNamedSprite(left, x, y, 0.009f, col, scale, scale, Flow.Draw.SpriteAlign.Centre, Flow.Draw.SpriteType.None, 1f, turn, format: any);
            draw.DrawNamedSprite(right, x, y, 0.009f, col, scale, scale, Flow.Draw.SpriteAlign.Centre, Flow.Draw.SpriteType.None, 1f, turn, format: any);
        }
    }

    /// <summary>
    /// RenderBattleLinePulse (004b82b0), from CHud::RenderBattleline at (x, y) while a message shows: the portrait's
    /// mouth changes every 0.05 s by the box's own stream (Simmons' at random, the others weighted), the portrait drawn
    /// six times with interference while it shows, the noise over it, and the interference easing towards the message's
    /// delay. While paused the stream is put back. The stream, frames and interference change whether or not anything
    /// is drawn.
    /// </summary>
    public void RenderBattleLinePulse(Flow.Draw.FrontEndDraw? draw, float x, float y, int a, float fps)
    {
        if (Current is not { } current) return;
        Game.Game? game = _context.Game;
        int seed = Random.Value;
        float level = Interference;
        if ((Random.Next() % 200) / 100.0f < Interference)
        {
            level = Interference + 0.2f;
            if (level < 1f) level = 1f;
        }

        float now = Events.Time;
        if (Portrait == 5)
        {
            if (PortraitTime + 0.05f < now)
            {
                PortraitTime = now;
                if (Random.Next() % 2 == 0)
                {
                    if (Random.Next() % 7 == 0) PortraitFrame = Random.Next() % 5;
                    PortraitFrame += PortraitFrame % 2 == 0 ? 1 : -1;
                    PortraitTexture = PortraitTextures[Portrait][PortraitFrame];
                }
            }
        }
        else if (PortraitTime + 0.05f < now)
        {
            PortraitTime = now;
            if (Random.Next() % 2 == 0)
            {
                int old = PortraitFrame;
                do
                {
                    float r = (float)((Random.Next() & 0xff) * (100.0 / 256.0));
                    PortraitFrame = r < 8f ? 0 : r < 20f ? 1 : r < 60f ? 2 : 3;
                }
                while (old == PortraitFrame);
                PortraitTexture = PortraitTextures[Portrait][PortraitFrame];
            }
        }

        if (!PortraitOff)
        {
            int alpha = 0x40;
            if (level > 0.3f) alpha = Random.Next() % 64;
            float v = level > 0.1f ? level * ((Random.Next() % 100) / 300.0f) : 0f;
            float v1 = v + 1f;
            float u = level > 0.1f ? level * ((Random.Next() % 100) / 500.0f) : 0f;
            float u1 = u + 1f;
            uint col = ((uint)alpha << 24) + 0xffffff;
            for (int i = 5; i >= 0; i--)
            {
                float su0 = u, su1 = u1;
                if (level > 0.2)
                {
                    float sx = level < 0.5f ? 0.5f : level; // Clamped(level, 0.5, level)
                    if (i % 2 == 0)
                    {
                        float start = u + sx * i * 0.16f;
                        su0 = start;
                        su1 = start + 1f;
                    }
                    else
                    {
                        su0 = u - sx * i * 0.16f;
                        su1 = 1f - sx * i * 0.16f + u;
                    }
                }

                if (PortraitTexture is { } texture)
                    draw?.DrawNamedSprite(texture, x + 16f, y - 16f, a / 1000f, col, 0.75f, 0.75f, (Flow.Draw.SpriteAlign)2,
                        Flow.Draw.SpriteType.None, 1f, 0f, su0, su1, v, v1, format: Flow.Draw.FrontEndTextures.AnyFormat);
            }
        }

        float nu = (Random.Next() % 100) / 100f;
        float nv = (Random.Next() % 100) / 100f;
        int noiseAlpha = (int)(((Random.Next() % 10) + 40) * (level + 1.5f));
        draw?.DrawNamedSprite(@"MessageBox\noisebig.tga", x, y, a / 1000f, ((uint)noiseAlpha << 24) + 0xffffff, 1f, 1f,
            (Flow.Draw.SpriteAlign)2, Flow.Draw.SpriteType.None, 1f, 0f, nu, nu + 1f, nv, nv + 1f, format: Flow.Draw.FrontEndTextures.AnyFormat);
        float target = current.Delay;
        if (!PortraitOff && level > target && game?.Paused != true)
        {
            Interference -= 60f / fps * 0.06f;
            if (Interference < target) Interference = target;
        }

        if (game?.Paused == true) Random.Value = seed;
    }
}
