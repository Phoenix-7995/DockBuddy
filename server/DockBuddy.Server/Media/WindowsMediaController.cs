using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using DockBuddy.Server.Platform;
using Windows.Media;
using Windows.Media.Control;

namespace DockBuddy.Server.Media;

/// <summary>
/// Reads and controls the Windows "now playing" media session (the one behind the volume flyout),
/// and checks the foreground window title to confirm that what is playing is a video.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public sealed class WindowsMediaController : IMediaController
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    private async Task<GlobalSystemMediaTransportControlsSessionManager> ManagerAsync()
        => _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

    /// <summary>Prefers a session that is actually playing, otherwise the system's current one.</summary>
    private async Task<GlobalSystemMediaTransportControlsSession?> PickAsync()
    {
        var mgr = await ManagerAsync();
        foreach (var s in mgr.GetSessions())
            if (s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                return s;
        return mgr.GetCurrentSession();
    }

    private static (string App, string Title, bool FullScreen) Foreground()
    {
        try
        {
            var hwnd = Win32.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return ("", "", false);
            var sb = new StringBuilder(512);
            Win32.GetWindowText(hwnd, sb, sb.Capacity);
            Win32.GetWindowThreadProcessId(hwnd, out var pid);
            var app = "";
            try { app = Process.GetProcessById((int)pid).ProcessName; } catch { }
            return (app, sb.ToString(), Win32.IsFullScreen(hwnd));
        }
        catch { return ("", "", false); }
    }

    private static string FriendlyApp(string appId)
    {
        var id = appId;
        var bang = id.LastIndexOf('!');
        if (bang >= 0) id = id[(bang + 1)..];
        if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) id = id[..^4];
        var l = id.ToLowerInvariant();
        if (l.Contains("chrome")) return "Chrome";
        if (l.Contains("msedge") || l == "edge") return "Edge";
        if (l.Contains("firefox")) return "Firefox";
        if (l.Contains("zunevideo")) return "Movies & TV";
        if (l.Contains("spotify")) return "Spotify";
        if (l.Contains("vlc")) return "VLC";
        return id;
    }

    public async Task<NowPlaying> GetAsync()
    {
        var (fgApp, fgTitle, fgFull) = Foreground();
        try
        {
            var s = await PickAsync();
            if (s == null) return new NowPlaying("none", "", "", "", false, "none", false, 0, 0, fgApp, fgTitle, fgFull);

            var info = s.GetPlaybackInfo();
            var status = info.PlaybackStatus switch
            {
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => "playing",
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => "paused",
                _ => "stopped"
            };
            var props = await s.TryGetMediaPropertiesAsync();
            var app = FriendlyApp(s.SourceAppUserModelId);
            var title = props?.Title ?? "";

            var timeline = s.GetTimelineProperties();
            var duration = (int)(timeline.EndTime - timeline.StartTime).TotalSeconds;
            var position = (int)(timeline.Position - timeline.StartTime).TotalSeconds;

            // 1st signal: the session says "video". 4th signal: the title/process/foreground window looks like a video site or player.
            var isVideoType = info.PlaybackType == MediaPlaybackType.Video;
            var sessionLooksVideo = VideoHeuristics.LooksLikeVideo(title, app);
            var foregroundConfirms = VideoHeuristics.IsBrowser(app) && VideoHeuristics.IsBrowser(fgApp)
                                     && VideoHeuristics.LooksLikeVideo(fgTitle, fgApp);
            var isVideo = status == "playing" && (isVideoType || sessionLooksVideo || foregroundConfirms);

            return new NowPlaying(status, app, title, props?.Artist ?? "", isVideo, "mediasession",
                info.Controls.IsPlaybackPositionEnabled && duration > 0, Math.Max(0, position), Math.Max(0, duration), fgApp, fgTitle, fgFull);
        }
        catch
        {
            return new NowPlaying("none", "", "", "", false, "none", false, 0, 0, fgApp, fgTitle, fgFull);
        }
    }

    public async Task<bool> TryCommandAsync(string command)
    {
        try
        {
            var s = await PickAsync();
            if (s == null) return false;
            return command switch
            {
                "playpause" => await s.TryTogglePlayPauseAsync(),
                "next" => await s.TrySkipNextAsync(),
                "prev" => await s.TrySkipPreviousAsync(),
                _ => false
            };
        }
        catch { return false; }
    }

    public async Task<bool> TrySeekAsync(int seconds)
    {
        try
        {
            var s = await PickAsync();
            if (s == null || !s.GetPlaybackInfo().Controls.IsPlaybackPositionEnabled) return false;
            var t = s.GetTimelineProperties();
            if (t.EndTime <= t.StartTime) return false;
            var target = t.Position + TimeSpan.FromSeconds(seconds);
            if (target < t.StartTime) target = t.StartTime;
            if (target > t.EndTime) target = t.EndTime;
            return await s.TryChangePlaybackPositionAsync(target.Ticks);
        }
        catch { return false; }
    }

    public async Task<bool> TrySeekToAsync(int seconds)
    {
        try
        {
            var s = await PickAsync();
            if (s == null || !s.GetPlaybackInfo().Controls.IsPlaybackPositionEnabled) return false;
            var t = s.GetTimelineProperties();
            if (t.EndTime <= t.StartTime) return false;
            var target = t.StartTime + TimeSpan.FromSeconds(seconds);
            if (target > t.EndTime) target = t.EndTime;
            return await s.TryChangePlaybackPositionAsync(target.Ticks);
        }
        catch { return false; }
    }
}
