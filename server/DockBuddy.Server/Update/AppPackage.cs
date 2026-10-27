using System.IO.Compression;
using System.Reflection;

namespace DockBuddy.Server.Update;

/// <param name="VersionCode">Android versionCode of the APK; phones update when this is higher than theirs.</param>
public record AppInfo(int VersionCode, string VersionName, long Size);

/// <summary>
/// The phone app this server hands out for remote updates: a DockBuddy.apk next to the exe (or the config folder),
/// otherwise the copy built into the exe.
/// </summary>
public static class AppPackage
{
    private const string FileName = "DockBuddy.apk";

    /// <summary>Opens the APK for reading, or null if this server has none.</summary>
    public static Stream? Open(string configDir)
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, configDir })
        {
            var path = Path.Combine(dir, FileName);
            if (File.Exists(path)) return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(FileName);
    }

    /// <summary>Version of the APK, read from the assets/version.txt the Gradle build puts inside it.</summary>
    public static AppInfo? Describe(string configDir)
    {
        using var apk = Open(configDir);
        if (apk == null) return null;
        try
        {
            using var zip = new ZipArchive(apk, ZipArchiveMode.Read, leaveOpen: true);
            var entry = zip.GetEntry("assets/version.txt");
            if (entry == null) return null;
            using var reader = new StreamReader(entry.Open());
            var lines = reader.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 1 || !int.TryParse(lines[0], out var code)) return null;
            return new AppInfo(code, lines.Length > 1 ? lines[1] : code.ToString(), apk.Length);
        }
        catch { return null; } // not a valid APK
    }
}
