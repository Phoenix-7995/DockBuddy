using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace DockBuddy.Server.Gui;

/// <summary>A program a page can be tied to: how it is named in the list, and the process name that is matched while it runs.</summary>
internal sealed record AppEntry(string Name, string Process)
{
    public override string ToString()
        => string.Equals(Name, Process, StringComparison.OrdinalIgnoreCase) ? Name : $"{Name}  ({Process})";
}

/// <summary>
/// Every program installed on the PC, not just the ones running: Start menu shortcuts, the "Uninstall" list
/// that Settings → Apps reads, and registered App Paths. Matched later by process name (the exe name without ".exe").
/// </summary>
[SupportedOSPlatform("windows")]
internal static class InstalledApps
{
    private static List<AppEntry>? _cache;

    /// <summary>Installers, uninstallers and updaters are not programs you would tie a page to.</summary>
    private static bool IsJunk(string process)
    {
        var p = process.ToLowerInvariant();
        return p.StartsWith("unins") || p.StartsWith("uninstall") || p.StartsWith("setup") || p.Contains("installer")
            || p is "update" or "updater" or "msiexec" or "crashpad_handler";
    }

    /// <summary>The installed programs (scanned once, then remembered), sorted by name.</summary>
    public static List<AppEntry> All() => _cache ??= Scan();

    /// <summary>Installed programs plus any running program with a window that isn't in that list.</summary>
    public static List<AppEntry> AllWithRunning()
    {
        var list = new List<AppEntry>(All());
        var known = list.Select(a => a.Process).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Platform.ForegroundApp.WithWindows())
            if (known.Add(name)) list.Add(new AppEntry(name, name));
        return list.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static List<AppEntry> Scan()
    {
        var found = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);

        void Add(string? name, string? exePath)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(exePath)) return;
            var process = Path.GetFileNameWithoutExtension(exePath.Trim());
            if (process.Length == 0 || process.Equals("DockBuddy.Server", StringComparison.OrdinalIgnoreCase)) return;
            if (IsJunk(process) || name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) return;
            found.TryAdd(process, new AppEntry(name.Trim(), process)); // first source wins; Start menu names come first
        }

        ScanStartMenu(Add);
        ScanUninstallList(Add);
        ScanAppPaths(Add);
        return found.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    // Start menu shortcuts: the name people know the program by, and the exe the shortcut starts.
    private static void ScanStartMenu(Action<string?, string?> add)
    {
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return;
        dynamic shell;
        try { shell = Activator.CreateInstance(shellType)!; } catch { return; }
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Environment.GetFolderPath(Environment.SpecialFolder.Programs) })
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
            foreach (var lnk in Directory.EnumerateFiles(root, "*.lnk", options))
            {
                try
                {
                    dynamic link = shell.CreateShortcut(lnk);
                    string target = link.TargetPath;
                    if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    // Squirrel-style apps (Discord, Slack, Teams...) start through "Update.exe --processStart App.exe".
                    if (Path.GetFileName(target).Equals("Update.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        string args = link.Arguments;
                        var m = System.Text.RegularExpressions.Regex.Match(args ?? "", @"--process-?[sS]tart\s+""?([^""\s]+\.exe)");
                        if (m.Success) target = m.Groups[1].Value;
                    }
                    add(Path.GetFileNameWithoutExtension(lnk), target);
                }
                catch { /* a broken shortcut */ }
            }
        }
    }

    // What Settings → Apps lists: DisplayName, and the exe from DisplayIcon when it points at one.
    private static void ScanUninstallList(Action<string?, string?> add)
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (var path in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            {
                try
                {
                    using var root = hive.OpenSubKey(path);
                    if (root == null) continue;
                    foreach (var sub in root.GetSubKeyNames())
                    {
                        using var k = root.OpenSubKey(sub);
                        var icon = (k?.GetValue("DisplayIcon") as string)?.Trim().Trim('"');
                        if (icon == null) continue;
                        var comma = icon.LastIndexOf(',');
                        if (comma > 0 && int.TryParse(icon[(comma + 1)..], out _)) icon = icon[..comma].Trim('"'); // "app.exe,0"
                        if (icon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) add(k!.GetValue("DisplayName") as string, icon);
                    }
                }
                catch { /* key missing or not readable */ }
            }
    }

    // Programs that registered their exe so Run/Win+R can find it.
    private static void ScanAppPaths(Action<string?, string?> add)
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var root = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
                if (root == null) continue;
                foreach (var sub in root.GetSubKeyNames().Where(s => s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                {
                    using var k = root.OpenSubKey(sub);
                    var exe = (k?.GetValue(null) as string)?.Trim().Trim('"') ?? sub;
                    string name;
                    try { name = FileVersionInfo.GetVersionInfo(exe).FileDescription ?? ""; } catch { name = ""; }
                    add(string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(sub) : name, sub);
                }
            }
            catch { }
        }
    }
}
