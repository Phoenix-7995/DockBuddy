using System.Runtime.InteropServices;

namespace DockBuddy.Server.Audio;

public interface IVolumeControl
{
    float GetLevel();           // 0..100
    void SetLevel(float level); // 0..100
    bool GetMute();
    void SetMute(bool mute);
}

/// <summary>Master volume (or, for the microphone, input level and mute) via Core Audio COM (IAudioEndpointVolume).</summary>
public sealed class WindowsVolumeControl : IVolumeControl
{
    private readonly int _dataFlow; // 0 = playback (speakers), 1 = capture (microphone)

    public WindowsVolumeControl(bool microphone = false) => _dataFlow = microphone ? 1 : 0;

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

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint count);
        int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        int GetMasterVolumeLevel(out float levelDb);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        int GetChannelVolumeLevel(uint channel, out float levelDb);
        int GetChannelVolumeLevelScalar(uint channel, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    private IAudioEndpointVolume Endpoint()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(_dataFlow, 1 /*multimedia*/, out var device));
        var iid = typeof(IAudioEndpointVolume).GUID;
        Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out var obj));
        return (IAudioEndpointVolume)obj;
    }

    private static Guid _ctx = Guid.Empty;

    public float GetLevel()
    {
        Marshal.ThrowExceptionForHR(Endpoint().GetMasterVolumeLevelScalar(out var v));
        return v * 100f;
    }

    public void SetLevel(float level)
    {
        level = Math.Clamp(level, 0f, 100f) / 100f;
        Marshal.ThrowExceptionForHR(Endpoint().SetMasterVolumeLevelScalar(level, ref _ctx));
    }

    public bool GetMute()
    {
        Marshal.ThrowExceptionForHR(Endpoint().GetMute(out var m));
        return m;
    }

    public void SetMute(bool mute) => Marshal.ThrowExceptionForHR(Endpoint().SetMute(mute, ref _ctx));
}

/// <summary>In-memory stand-in used by --dev mode on non-Windows hosts.</summary>
public sealed class FakeVolumeControl : IVolumeControl
{
    private float _level = 50;
    private bool _mute;
    public float GetLevel() => _level;
    public void SetLevel(float level) => _level = Math.Clamp(level, 0, 100);
    public bool GetMute() => _mute;
    public void SetMute(bool mute) => _mute = mute;
}
