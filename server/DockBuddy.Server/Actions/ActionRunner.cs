using System.Diagnostics;
using System.Text;
using DockBuddy.Server.Audio;
using DockBuddy.Server.Media;
using DockBuddy.Server.Platform;

namespace DockBuddy.Server.Actions;

public record ActionRequest(string Type, string? Value, string? Args);
public record ActionResult(bool Ok, string? Output = null, string? Error = null, bool? On = null);

public sealed class ActionRunner
{
    private readonly IVolumeControl _volume;
    private readonly IVolumeControl _mic;
    private readonly IMediaController _media;
    private readonly bool _dev;
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    public ActionRunner(IVolumeControl volume, IVolumeControl mic, IMediaController media, bool dev)
    {
        _volume = volume;
        _mic = mic;
        _media = media;
        _dev = dev;
    }

    public async Task<ActionResult> RunAsync(ActionRequest a)
    {
        try
        {
            return a.Type.ToLowerInvariant() switch
            {
                "open" => Open(a.Value ?? "", a.Args),
                "command" => await RunCommandAsync(a.Value ?? ""),
                "script" => await RunScriptAsync(a.Value ?? "", a.Args),
                "keys" => Keys(a.Value ?? ""),
                "system" => SystemAction(a.Value ?? ""),
                "volume" => Volume(a.Value ?? ""),
                "media" => await MediaAsync(a.Value ?? ""),
                _ => new ActionResult(false, Error: $"Unknown action type '{a.Type}'")
            };
        }
        catch (Exception ex)
        {
            return new ActionResult(false, Error: ex.Message);
        }
    }

    private const int MaxMacroWaitMs = 30_000;

    /// <summary>Runs a deck button: a plain action, or all steps of a macro in order (stops at the first failure).</summary>
    public Task<ActionResult> RunButtonAsync(DeckButton b)
        => b.Type.Equals("macro", StringComparison.OrdinalIgnoreCase)
            ? RunMacroAsync(b.Steps ?? new())
            : RunAsync(new ActionRequest(b.Type, b.Value, b.Args));

    private async Task<ActionResult> RunMacroAsync(List<MacroStep> steps)
    {
        if (steps.Count == 0) return new ActionResult(false, Error: "This macro has no steps");
        var output = new StringBuilder();
        var waitLeft = MaxMacroWaitMs;
        for (var i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            var r = await RunAsync(new ActionRequest(s.Type, s.Value, s.Args));
            if (!string.IsNullOrWhiteSpace(r.Output)) output.AppendLine(r.Output.Trim());
            if (!r.Ok) return new ActionResult(false, output.ToString().Trim(), $"Step {i + 1} ({s.Type}): {r.Error}");
            var wait = Math.Clamp(s.DelayMs, 0, waitLeft);
            if (wait > 0 && i < steps.Count - 1) { waitLeft -= wait; await Task.Delay(wait); }
        }
        return new ActionResult(true, output.Length > 0 ? output.ToString().Trim() : null);
    }

    // ---------- open ----------
    private ActionResult Open(string target, string? args)
    {
        if (string.IsNullOrWhiteSpace(target)) return new(false, Error: "No target given");
        if (_dev) return new(true, $"[dev] would open {target} {args}");
        var psi = new ProcessStartInfo(target) { UseShellExecute = true };
        if (!string.IsNullOrWhiteSpace(args)) psi.Arguments = args;
        Process.Start(psi);
        return new(true);
    }

    // ---------- command / script ----------
    private Task<ActionResult> RunCommandAsync(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return Task.FromResult(new ActionResult(false, Error: "Empty command"));
        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", cmd } }
            : new ProcessStartInfo("/bin/sh") { ArgumentList = { "-c", cmd } };
        return RunProcessAsync(psi);
    }

    private Task<ActionResult> RunScriptAsync(string path, string? args)
    {
        if (!File.Exists(path)) return Task.FromResult(new ActionResult(false, Error: $"Script not found: {path}"));
        ProcessStartInfo psi;
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".ps1":
                psi = new ProcessStartInfo("powershell.exe") { ArgumentList = { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", path } };
                break;
            case ".bat":
            case ".cmd":
                psi = new ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", path } };
                break;
            case ".py":
                psi = new ProcessStartInfo(OperatingSystem.IsWindows() ? "python" : "python3") { ArgumentList = { path } };
                break;
            case ".sh":
                psi = new ProcessStartInfo("/bin/sh") { ArgumentList = { path } };
                break;
            default:
                psi = new ProcessStartInfo(path);
                break;
        }
        if (!string.IsNullOrWhiteSpace(args))
            foreach (var part in args.Split(' ', StringSplitOptions.RemoveEmptyEntries)) psi.ArgumentList.Add(part);
        return RunProcessAsync(psi);
    }

    private static async Task<ActionResult> RunProcessAsync(ProcessStartInfo psi)
    {
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process");
        var sb = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using var cts = new CancellationTokenSource(CommandTimeout);
        try
        {
            await p.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { }
            return new(false, Truncate(sb.ToString()), "Timed out after 30s");
        }
        var output = Truncate(sb.ToString());
        return p.ExitCode == 0 ? new(true, output) : new(false, output, $"Exit code {p.ExitCode}");
    }

    private static string Truncate(string s) => s.Length > 4000 ? s[..4000] + "\n…" : s;

    // ---------- keys ----------
    private static readonly Dictionary<string, ushort> VkNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = 0x11, ["control"] = 0x11, ["shift"] = 0x10, ["alt"] = 0x12, ["win"] = 0x5B, ["super"] = 0x5B,
        ["esc"] = 0x1B, ["escape"] = 0x1B, ["enter"] = 0x0D, ["return"] = 0x0D, ["tab"] = 0x09, ["space"] = 0x20,
        ["backspace"] = 0x08, ["delete"] = 0x2E, ["del"] = 0x2E, ["insert"] = 0x2D, ["home"] = 0x24, ["end"] = 0x23,
        ["pageup"] = 0x21, ["pagedown"] = 0x22, ["left"] = 0x25, ["up"] = 0x26, ["right"] = 0x27, ["down"] = 0x28,
        ["printscreen"] = 0x2C, ["prtsc"] = 0x2C,
        ["media_play_pause"] = 0xB3, ["media_next"] = 0xB0, ["media_prev"] = 0xB1, ["media_stop"] = 0xB2,
        ["volume_up"] = 0xAF, ["volume_down"] = 0xAE, ["volume_mute"] = 0xAD,
    };

    private static readonly HashSet<ushort> ExtendedKeys = new() { 0x2D, 0x2E, 0x24, 0x23, 0x21, 0x22, 0x25, 0x26, 0x27, 0x28, 0x5B };

    public static bool TryParseKeys(string combo, out List<ushort> vks, out string error)
    {
        vks = new();
        error = "";
        foreach (var raw in combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (VkNames.TryGetValue(raw, out var vk)) vks.Add(vk);
            else if (raw.Length == 1 && char.IsAsciiLetterOrDigit(raw[0])) vks.Add((ushort)char.ToUpperInvariant(raw[0]));
            else if (raw.Length >= 2 && (raw[0] is 'f' or 'F') && int.TryParse(raw.AsSpan(1), out var n) && n is >= 1 and <= 24)
                vks.Add((ushort)(0x70 + n - 1));
            else { error = $"Unknown key '{raw}'"; return false; }
        }
        if (vks.Count == 0) { error = "No keys given"; return false; }
        return true;
    }

    private ActionResult Keys(string combo)
    {
        if (!TryParseKeys(combo, out var vks, out var err)) return new(false, Error: err);
        if (_dev) return new(true, $"[dev] would press {string.Join("+", vks.Select(v => "0x" + v.ToString("X2")))}");
        if (!OperatingSystem.IsWindows()) return new(false, Error: "Keys are only supported on Windows");

        var inputs = new List<Win32.INPUT>();
        foreach (var vk in vks) inputs.Add(Key(vk, false));
        for (int i = vks.Count - 1; i >= 0; i--) inputs.Add(Key(vks[i], true));
        var sent = Win32.SendInput((uint)inputs.Count, inputs.ToArray(), System.Runtime.InteropServices.Marshal.SizeOf<Win32.INPUT>());
        return sent == inputs.Count ? new(true) : new(false, Error: "SendInput was blocked (is a UAC prompt or elevated window in focus?)");
    }

    private static Win32.INPUT Key(ushort vk, bool up)
    {
        uint flags = up ? Win32.KEYEVENTF_KEYUP : 0;
        if (ExtendedKeys.Contains(vk)) flags |= Win32.KEYEVENTF_EXTENDEDKEY;
        return new Win32.INPUT { type = Win32.INPUT_KEYBOARD, u = new Win32.INPUTUNION { ki = new Win32.KEYBDINPUT { wVk = vk, dwFlags = flags } } };
    }

    // ---------- system ----------
    private ActionResult SystemAction(string what)
    {
        var w = what.ToLowerInvariant();
        if (_dev) return new(true, $"[dev] would run system action {w}");
        if (!OperatingSystem.IsWindows()) return new(false, Error: "System actions are only supported on Windows");
        switch (w)
        {
            case "lock": Win32.LockWorkStation(); return new(true);
            case "sleep": Win32.SetSuspendState(false, false, false); return new(true);
            case "hibernate": Win32.SetSuspendState(true, false, false); return new(true);
            case "screenoff":
                Win32.SendMessage(Win32.HWND_BROADCAST, Win32.WM_SYSCOMMAND, Win32.SC_MONITORPOWER, new IntPtr(2));
                return new(true);
            case "shutdown": Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") { CreateNoWindow = true }); return new(true);
            case "restart": Process.Start(new ProcessStartInfo("shutdown", "/r /t 0") { CreateNoWindow = true }); return new(true);
            case "logoff": Process.Start(new ProcessStartInfo("shutdown", "/l") { CreateNoWindow = true }); return new(true);
            default: return new(false, Error: $"Unknown system action '{what}'");
        }
    }

    // ---------- media ----------
    /// <summary>
    /// playpause | next | prev | seek:-10 | seek:+10 | seekto:123 (absolute seconds). Tries the Windows media session first so it hits the
    /// app that is playing, and falls back to media keys / arrow keys for players that don't expose one.
    /// </summary>
    private async Task<ActionResult> MediaAsync(string cmd)
    {
        if (cmd.StartsWith("seekto:", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(cmd[7..], out var at)) return new(false, Error: $"Bad seek '{cmd}'");
            return await _media.TrySeekToAsync(at) ? new(true) : new(false, Error: "This player can't jump to a position");
        }
        if (cmd.StartsWith("seek:", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(cmd[5..], out var secs)) return new(false, Error: $"Bad seek '{cmd}'");
            if (await _media.TrySeekAsync(secs)) return new(true);
            return Keys(secs < 0 ? "left" : "right");
        }
        if (await _media.TryCommandAsync(cmd)) return new(true);
        return cmd switch
        {
            "playpause" => Keys("media_play_pause"),
            "next" => Keys("media_next"),
            "prev" => Keys("media_prev"),
            _ => new(false, Error: $"Unknown media command '{cmd}'")
        };
    }

    // ---------- volume ----------
    public VolumeState GetVolume() => new((int)Math.Round(_volume.GetLevel()), _volume.GetMute());

    private static ActionResult MicResult(VolumeState s) => new(true, s.Muted ? "Microphone muted" : "Microphone on");

    public VolumeState GetMic() => new((int)Math.Round(_mic.GetLevel()), _mic.GetMute());

    /// <summary>Mutes, unmutes or toggles the microphone (null = leave as is) and/or sets its input level.</summary>
    public VolumeState SetMic(bool? mute, bool toggle, int? level)
    {
        if (level.HasValue) _mic.SetLevel(level.Value);
        if (toggle) _mic.SetMute(!_mic.GetMute());
        else if (mute.HasValue) _mic.SetMute(mute.Value);
        return GetMic();
    }

    public ActionResult Volume(string what)
    {
        switch (what.ToLowerInvariant())
        {
            case "mic-mute": return MicResult(SetMic(true, false, null));
            case "mic-unmute": return MicResult(SetMic(false, false, null));
            case "mic-toggle": return MicResult(SetMic(null, true, null));
            case "up": _volume.SetLevel(_volume.GetLevel() + 5); break;
            case "down": _volume.SetLevel(_volume.GetLevel() - 5); break;
            case "mute": _volume.SetMute(true); break;
            case "unmute": _volume.SetMute(false); break;
            case "toggle-mute": _volume.SetMute(!_volume.GetMute()); break;
            default:
                if (float.TryParse(what, out var lvl)) { _volume.SetLevel(lvl); break; }
                return new(false, Error: $"Unknown volume action '{what}'");
        }
        var s = GetVolume();
        return new(true, $"{s.Level}%{(s.Muted ? " (muted)" : "")}");
    }
}

public record VolumeState(int Level, bool Muted);
