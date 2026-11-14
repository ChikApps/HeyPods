// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using Earbuds.Core.Platform;
using Earbuds.Core.Playback;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Earbuds.Core.Tests;

public class AutoPauseTests
{
    private readonly FakeMediaSessions _media = new();
    private readonly FakeTimeProvider _time = new();
    private readonly AutoPauseController _c;

    public AutoPauseTests()
    {
        _c = new AutoPauseController(_media, _time);
        _media.Sessions["spotify"] = MediaPlaybackStatus.Playing;
    }

    private Task<AutoPauseAction> Read(int? count, bool enabled = true, bool connected = true) =>
        _c.OnWearReadingAsync(count, enabled, connected);

    [Fact]
    public async Task OneOut_Pauses_BothBack_Resumes()
    {
        Assert.Equal(AutoPauseAction.None, await Read(2));
        Assert.Equal(AutoPauseAction.Paused, await Read(1));
        Assert.Equal(MediaPlaybackStatus.Paused, _media.Sessions["spotify"]);
        Assert.Equal(AutoPauseAction.Resumed, await Read(2));
        Assert.Equal(MediaPlaybackStatus.Playing, _media.Sessions["spotify"]);
        Assert.Equal(new[] { "pause:spotify", "play:spotify" }, _media.Calls);
    }

    [Fact]
    public async Task OneOut_ThenBothOut_ThenOneBack_DoesNotResumeYet()
    {
        await Read(2);
        Assert.Equal(AutoPauseAction.Paused, await Read(1));
        Assert.Equal(AutoPauseAction.None, await Read(0));
        Assert.Equal(AutoPauseAction.None, await Read(1));
        Assert.Equal(AutoPauseAction.Resumed, await Read(2));
        Assert.Single(_media.Calls, "pause:spotify");
    }

    [Fact] // spec: "user paused by hand" -> never start playback
    public async Task UserPausedBeforeRemoving_NoResume()
    {
        await Read(2);
        _media.UserSets("spotify", MediaPlaybackStatus.Paused);
        Assert.Equal(AutoPauseAction.None, await Read(1));
        Assert.Equal(AutoPauseAction.None, await Read(2));
        Assert.Empty(_media.Calls);
        Assert.Equal(MediaPlaybackStatus.Paused, _media.Sessions["spotify"]);
    }

    [Fact]
    public async Task UserResumedWhileOut_NotTouchedAgain()
    {
        await Read(2);
        await Read(1);
        _time.Advance(TimeSpan.FromSeconds(5));
        _media.UserSets("spotify", MediaPlaybackStatus.Playing); // user played from the keyboard
        _media.UserSets("spotify", MediaPlaybackStatus.Paused);  // ...and paused again
        Assert.Null(_c.PausedSessionId);
        Assert.Equal(AutoPauseAction.None, await Read(2));
        Assert.Equal(MediaPlaybackStatus.Paused, _media.Sessions["spotify"]);
    }

    [Fact]
    public async Task StatusEventRightAfterOurPause_IsIgnored()
    {
        await Read(2);
        await Read(1);
        _media.UserSets("spotify", MediaPlaybackStatus.Playing); // late event from before our pause
        Assert.Equal("spotify", _c.PausedSessionId);
    }

    [Fact]
    public async Task UserStoppedWhileOut_NoResume()
    {
        await Read(2);
        await Read(1);
        _media.Sessions["spotify"] = MediaPlaybackStatus.Stopped;
        Assert.Equal(AutoPauseAction.None, await Read(2));
        Assert.Equal(new[] { "pause:spotify" }, _media.Calls);
    }

    [Fact]
    public async Task PlayerClosedWhileOut_NoResume()
    {
        await Read(2);
        await Read(1);
        _media.Sessions.Remove("spotify");
        Assert.Equal(AutoPauseAction.None, await Read(2));
    }

    [Fact] // spec: no active player -> do nothing (no toggle key)
    public async Task NothingPlaying_DoesNothing()
    {
        _media.Sessions.Clear();
        await Read(2);
        Assert.Equal(AutoPauseAction.None, await Read(1));
        Assert.Equal(AutoPauseAction.None, await Read(2));
        Assert.Empty(_media.Calls);
    }

    [Fact] // spec §5 layer 3
    public async Task NotConnectedAsAudio_NeverPauses()
    {
        await Read(2, connected: false);
        Assert.Equal(AutoPauseAction.None, await Read(1, connected: false));
        Assert.Empty(_media.Calls);
    }

    [Fact] // spec §6: off -> neither pause nor resume
    public async Task Disabled_NeitherPausesNorResumes()
    {
        await Read(2);
        await Read(1); // paused while enabled
        Assert.Equal(AutoPauseAction.None, await Read(1, enabled: false));
        Assert.Equal(AutoPauseAction.None, await Read(2, enabled: false));
        Assert.Equal(new[] { "pause:spotify" }, _media.Calls);
    }

    [Fact] // starting the app with pods already out must not pause anything
    public async Task FirstReading_NoAction()
    {
        Assert.Equal(AutoPauseAction.None, await Read(0));
        Assert.Equal(AutoPauseAction.None, await Read(0));
        Assert.Empty(_media.Calls);
    }

    [Fact]
    public async Task UnknownReading_Ignored()
    {
        await Read(2);
        Assert.Equal(AutoPauseAction.None, await Read(null));
        Assert.Equal(AutoPauseAction.Paused, await Read(1));
    }

    [Fact] // listening with a single pod (other one in the case)
    public async Task SinglePodListening_PausesAndResumes()
    {
        await Read(1);
        Assert.Equal(AutoPauseAction.Paused, await Read(0));
        Assert.Equal(AutoPauseAction.Resumed, await Read(1));
    }

    [Fact]
    public async Task MediaApiThrows_NoCrash()
    {
        await Read(2);
        _media.FailCalls = true;
        Assert.Equal(AutoPauseAction.None, await Read(1));
    }

    [Fact]
    public async Task Reset_ForgetsPause()
    {
        await Read(2);
        await Read(1);
        _c.Reset();
        Assert.Equal(AutoPauseAction.None, await Read(2));
        Assert.Equal(AutoPauseAction.None, await Read(2));
    }
}
