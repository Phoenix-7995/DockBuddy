namespace DockBuddy.Server.Media;

/// <param name="Status">playing, paused, stopped or none</param>
public record NowPlaying(
    string Status, string App, string Title, string Artist, bool IsVideo, string Source, bool CanSeek,
    int PositionSeconds, int DurationSeconds, string ForegroundApp, string ForegroundTitle,
    bool IsFullScreen = false);

public interface IMediaController
{
    Task<NowPlaying> GetAsync();

    /// <summary>playpause, next or prev on the current media session. False if no session could handle it.</summary>
    Task<bool> TryCommandAsync(string command);

    /// <summary>Seeks the current media session by a number of seconds. False if it can't seek.</summary>
    Task<bool> TrySeekAsync(int seconds);

    /// <summary>Jumps to an absolute position in seconds. False if it can't seek.</summary>
    Task<bool> TrySeekToAsync(int seconds);
}

/// <summary>Used on Windows versions without the media session API: reports nothing playing.</summary>
public sealed class NullMediaController : IMediaController
{
    public Task<NowPlaying> GetAsync() => Task.FromResult(new NowPlaying("none", "", "", "", false, "none", false, 0, 0, "", ""));
    public Task<bool> TryCommandAsync(string command) => Task.FromResult(false);
    public Task<bool> TrySeekAsync(int seconds) => Task.FromResult(false);
    public Task<bool> TrySeekToAsync(int seconds) => Task.FromResult(false);
}

/// <summary>Pretends a video is playing so the phone UI can be exercised without a Windows PC (--dev).</summary>
public sealed class FakeMediaController : IMediaController
{
    private const int Duration = 596;
    private bool _playing = true;
    private double _position = 42;
    private DateTime _last = DateTime.UtcNow;

    private void Tick()
    {
        var now = DateTime.UtcNow;
        if (_playing) _position = Math.Min(Duration, _position + (now - _last).TotalSeconds);
        _last = now;
    }

    public Task<NowPlaying> GetAsync()
    {
        Tick();
        return Task.FromResult(new NowPlaying(_playing ? "playing" : "paused", "VLC", "Big Buck Bunny", "",
            true, "mediasession", true, (int)_position, Duration, "vlc", "Big Buck Bunny - VLC media player"));
    }

    public Task<bool> TryCommandAsync(string command)
    {
        Tick();
        if (command == "playpause") _playing = !_playing;
        return Task.FromResult(command is "playpause" or "next" or "prev");
    }

    public Task<bool> TrySeekAsync(int seconds)
    {
        Tick();
        _position = Math.Clamp(_position + seconds, 0, Duration);
        return Task.FromResult(true);
    }

    public Task<bool> TrySeekToAsync(int seconds)
    {
        Tick();
        _position = Math.Clamp(seconds, 0, Duration);
        return Task.FromResult(true);
    }
}

/// <summary>Heuristics on window titles and process names, used to confirm that what is playing is a video.</summary>
public static class VideoHeuristics
{
    private static readonly string[] SiteWords =
    {
        "youtube", "netflix", "twitch", "prime video", "primevideo", "disney+", "disneyplus", "hulu", "hbo max",
        "max.com", "crunchyroll", "vimeo", "apple tv", "peacock", "paramount+", "dailymotion", "plex", "jellyfin",
        "emby", "kick.com", "rumble"
    };

    private static readonly string[] PlayerProcesses =
    {
        "vlc", "mpv", "mpc-hc", "mpc-hc64", "mpc-be", "mpc-be64", "potplayer", "potplayermini", "wmplayer", "video.ui",
        "kodi", "plex", "netflix", "primevideo", "disneyplus", "movies"
    };

    private static readonly string[] Browsers = { "chrome", "msedge", "edge", "firefox", "opera", "brave", "vivaldi" };

    public static bool IsBrowser(string name) => Browsers.Any(b => name.Contains(b, StringComparison.OrdinalIgnoreCase));

    public static bool LooksLikeVideo(string title, string processName)
    {
        var t = title.ToLowerInvariant();
        var p = processName.ToLowerInvariant();
        return SiteWords.Any(t.Contains) || PlayerProcesses.Any(p.Contains);
    }
}
