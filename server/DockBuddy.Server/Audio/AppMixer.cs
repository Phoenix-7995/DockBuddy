using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DockBuddy.Server.Audio;

/// <param name="Name">Program name (e.g. "chrome"); also the id used to change it.</param>
/// <param name="Volume">0..100</param>
public record MixerApp(string Name, int Volume, bool Muted, bool Active);

/// <summary>Per-application volume, like the Windows volume mixer.</summary>
public interface IAppMixer
{
    List<MixerApp> List();

    /// <summary>Sets the level (0..100) and/or mute of every audio session of the named program. False if it has none.</summary>
    bool Set(string name, int? level, bool? mute);
}

/// <summary>Per-app volume through the Core Audio session API on the default playback device.</summary>
public sealed class WindowsAppMixer : IAppMixer
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        // IAudioSessionManager
        int GetAudioSessionControl(IntPtr groupId, uint flags, out IntPtr session);
        int GetSimpleAudioVolume(IntPtr groupId, uint flags, out IntPtr volume);
        // IAudioSessionManager2
        int GetSessionEnumerator(out IAudioSessionEnumerator enumerator);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        int GetCount(out int count);
        int GetSession(int index, out IAudioSessionControl session);
    }

    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        int GetState(out int state); // 0 inactive, 1 active, 2 expired
    }

    // The full vtable (IAudioSessionControl's nine methods first); only GetProcessId and IsSystemSoundsSession are called.
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        int GetState(out int state);
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid ctx);
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid ctx);
        int GetGroupingParam(out Guid param);
        int SetGroupingParam(ref Guid param, ref Guid ctx);
        int RegisterAudioSessionNotification(IntPtr notify);
        int UnregisterAudioSessionNotification(IntPtr notify);
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
        int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        int SetMasterVolume(float level, ref Guid ctx);
        int GetMasterVolume(out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    private sealed record Session(string Name, ISimpleAudioVolume Volume, int State);

    private static Guid _ctx = Guid.Empty;

    private static IAudioSessionManager2 Manager()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0 /*render*/, 1 /*multimedia*/, out var device));
        var iid = typeof(IAudioSessionManager2).GUID;
        Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out var obj));
        return (IAudioSessionManager2)obj;
    }

    private static string ProcessName(uint pid)
    {
        try { using var p = Process.GetProcessById((int)pid); return p.ProcessName; }
        catch { return ""; }
    }

    private static List<Session> Sessions()
    {
        var list = new List<Session>();
        Marshal.ThrowExceptionForHR(Manager().GetSessionEnumerator(out var sessions));
        Marshal.ThrowExceptionForHR(sessions.GetCount(out var count));
        for (var i = 0; i < count; i++)
        {
            try
            {
                if (sessions.GetSession(i, out var control) != 0) continue;
                if (control is not IAudioSessionControl2 c2 || control is not ISimpleAudioVolume vol) continue;
                if (c2.IsSystemSoundsSession() == 0) continue; // S_OK = this is the "System sounds" session
                c2.GetState(out var state);
                if (state == 2) continue; // expired
                c2.GetProcessId(out var pid);
                var name = ProcessName(pid);
                if (name.Length == 0) continue;
                list.Add(new Session(name, vol, state));
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException)
            {
                // One odd session (a closing app, a protected process) must not hide the others.
            }
        }
        return list;
    }

    public List<MixerApp> List()
    {
        return Sessions()
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                float level = 0;
                var muted = true;
                foreach (var s in g)
                {
                    try
                    {
                        s.Volume.GetMasterVolume(out var v);
                        s.Volume.GetMute(out var m);
                        level = Math.Max(level, v);
                        muted &= m;
                    }
                    catch (COMException) { /* session ended while reading */ }
                }
                return new MixerApp(g.Key, (int)Math.Round(level * 100), muted, g.Any(s => s.State == 1));
            })
            .OrderByDescending(a => a.Active).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool Set(string name, int? level, bool? mute)
    {
        var found = false;
        foreach (var s in Sessions().Where(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            found = true;
            try
            {
                if (level.HasValue) s.Volume.SetMasterVolume(Math.Clamp(level.Value, 0, 100) / 100f, ref _ctx);
                if (mute.HasValue) s.Volume.SetMute(mute.Value, ref _ctx);
            }
            catch (COMException) { /* session ended */ }
        }
        return found;
    }
}

/// <summary>A few pretend apps for --dev mode on non-Windows hosts.</summary>
public sealed class FakeAppMixer : IAppMixer
{
    private readonly Dictionary<string, (int Level, bool Muted)> _apps = new()
    {
        ["chrome"] = (70, false), ["spotify"] = (45, false), ["discord"] = (100, false), ["vlc"] = (30, true)
    };

    public List<MixerApp> List() => _apps.Select(a => new MixerApp(a.Key, a.Value.Level, a.Value.Muted, true)).ToList();

    public bool Set(string name, int? level, bool? mute)
    {
        if (!_apps.TryGetValue(name, out var cur)) return false;
        _apps[name] = (level ?? cur.Level, mute ?? cur.Muted);
        return true;
    }
}
