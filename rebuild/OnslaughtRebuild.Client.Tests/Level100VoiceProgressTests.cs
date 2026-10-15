// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the properties that make the First Flight smoke's voice assertions
/// legitimate. The smoke cannot assert "a message is audible at tick N":
/// AudioStreamPlayer playback advances on the mixer thread in wall-clock
/// seconds while --fixed-fps advances the simulation as fast as the host
/// allows, so that value is decided by host speed. What it asserts instead is
/// that the ordered message IDs the voice adapter has actually started form an
/// exact prefix of the Core-requested sequence. These tests pin why that bound
/// is sound: the character message queue is strictly FIFO regardless of when
/// the mixer drains it, so host speed can only change how far the prefix got,
/// never its content or order.
/// </summary>
public sealed class Level100VoiceProgressTests
{
    // The ordered IDs Core requests during the smoke window, as pinned by
    // the smoke report contract (rebuild/tools/first_flight.py) for
    // level100DeliveredMessageIds.
    private static readonly int[] SmokeRequestedMessageIds =
    [
        292_562, 293_386, 296_682, -1_575_499_396, -257_967_449, 82_987_417,
        4_422_830, 175_347_826, 4_458_134, 4_493_438, 295_858, 1_339_691_000,
        669_198_996, -1_715_818_922,
    ];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(13)]
    public void StartedMessagesAreAlwaysAnOrderedPrefixOfTheRequestedSequence(
        int mixerLag)
    {
        var queue = new Level100CharacterMessageQueue();
        var started = new List<int>();

        // The mixer is arbitrarily far behind the script: enqueue every
        // requested message, draining only once the backlog exceeds the lag.
        for (int index = 0; index < SmokeRequestedMessageIds.Length; index++)
        {
            queue.Enqueue(4_242, SmokeRequestedMessageIds[index]);
            while (queue.Count > mixerLag &&
                   queue.TryDequeue(out Level100QueuedCharacterMessage drained))
            {
                started.Add(drained.Audio.MessageId);
            }

            // The invariant the smoke gate relies on holds at every instant,
            // not just at the end: whatever has started so far is the exact
            // ordered prefix of what was requested so far.
            Assert.Equal(
                SmokeRequestedMessageIds.Take(started.Count),
                started);
        }

        Assert.NotEmpty(started);
        Assert.Equal(SmokeRequestedMessageIds.Length - mixerLag, started.Count);
    }

    [Fact]
    public void SpeakerIdentitySurvivesTheQueueUnchangedAndInOrder()
    {
        // The smoke pins level100AudioQueuedSpeakerIds, which is only sound if
        // the queue carries speaker identity through unchanged alongside the
        // message. One of the fourteen smoke messages has a different speaker.
        int[] speakers = [.. SmokeRequestedMessageIds.Select((_, index) =>
            index == 4 ? 10_565_784 : 1_508_464)];

        var queue = new Level100CharacterMessageQueue();
        for (int index = 0; index < SmokeRequestedMessageIds.Length; index++)
        {
            queue.Enqueue(speakers[index], SmokeRequestedMessageIds[index]);
        }

        var drainedSpeakers = new List<int>();
        var drainedMessages = new List<int>();
        while (queue.TryDequeue(out Level100QueuedCharacterMessage message))
        {
            drainedSpeakers.Add(message.SpeakerId);
            drainedMessages.Add(message.Audio.MessageId);
        }

        Assert.Equal(speakers, drainedSpeakers);
        Assert.Equal(SmokeRequestedMessageIds, drainedMessages);
    }

    [Fact]
    public void EveryRequestedSmokeMessageResolvesToAPlayableStreamSoTheBoundIsNotCapped()
    {
        // Non-vacuity of the prefix bound: the observed prefix is short only
        // because the mixer is slower than the simulation, never because a
        // later message could not be started at all.
        foreach (int messageId in SmokeRequestedMessageIds)
        {
            Level100MessageAudioSpec spec =
                Level100AudioCatalog.GetCharacterMessage(messageId);
            Assert.Equal(messageId, spec.MessageId);
            Assert.False(string.IsNullOrWhiteSpace(spec.ResourcePath));
        }
    }

    [Fact]
    public void ClearingTheQueueCannotLeaveAStaleMessageBehind()
    {
        var queue = new Level100CharacterMessageQueue();
        foreach (int messageId in SmokeRequestedMessageIds)
        {
            queue.Enqueue(1_508_464, messageId);
        }

        queue.Clear();

        Assert.Equal(0, queue.Count);
        Assert.False(queue.TryDequeue(out _));
    }
}
