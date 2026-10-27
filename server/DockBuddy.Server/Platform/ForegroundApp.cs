using System.Diagnostics;

namespace DockBuddy.Server.Platform;

/// <summary>Which program is in front on the PC, and which programs can be picked to anchor a page to.</summary>
public static class ForegroundApp
{
    /// <summary>Process name (no .exe) of the foreground window; empty when unknown or not on Windows.</summary>
    public static string Current()
    {
        if (!OperatingSystem.IsWindows()) return "";
        try
        {
            var hwnd = Win32.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return "";
            Win32.GetWindowThreadProcessId(hwnd, out var pid);
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch { return ""; }
    }

    /// <summary>Names of running programs that have a visible window, sorted.</summary>
    public static string[] WithWindows()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try { if (p.MainWindowHandle != IntPtr.Zero && p.MainWindowTitle.Length > 0) names.Add(p.ProcessName); }
            catch { /* process exited or is protected */ }
            finally { p.Dispose(); }
        }
        return names.ToArray();
    }
}
