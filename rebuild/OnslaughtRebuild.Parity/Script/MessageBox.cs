// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/MessageBox.cpp and MessageBox.h (CMessage 004b6e50, ~CMessage 004b7160,
// SampleFinishedPlaying 004b71d0; CMessageBox 004b71e0, ~CMessageBox 004b7930, SwitchMessagesOn 004b7b60,
// SwitchMessagesOff 004b7b70, TryAdvance 004b7b80, AddMessage 004b7ca0, PlayPendingSamples 004b7d90, StartMessage
// 004b7ea0, AdvanceReveal 004b8020, HandleEvent 004b81d0, StopCurrentPlayingSound 004b8800) and src/SPtrSet.cpp
// (GenericSPtrSet::operator= 004e58a0), reconstructed from the retail executable; not in Stuart's release. The queue and
// its timing only: portraits, word wrap and drawing are the renderer's.

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
            VoiceName = null;
            if (m.Speaker.ToRead is not null)
            {
                _context.Skip("CHud::SetMessageSpeaker");
            }

            _context.Skip("CMessageBox::GetPortrait (display)");
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
        if (m.SpeakerGone() && m.Revealed < m.Text.Length)
        {
            StopCurrentPlayingSound();
            m.Dots++;
            if (m.Dots < 5)
            {
                Events.AddEventIn(0.038f, Reveal, this);
                return;
            }

            Events.AddEventIn(2.7f, Finish, this);
            return;
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

        Events.AddEventIn(0.3f, Finish, this);
    }

    /// <summary>StopCurrentPlayingSound (with CLIPARAMS.mUnknown18c clear): the voice is forgotten and killed.</summary>
    public void StopCurrentPlayingSound()
    {
        VoiceName = null;
        Voice.KillVoice();
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
}
