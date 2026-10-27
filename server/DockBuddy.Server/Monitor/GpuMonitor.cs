using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace DockBuddy.Server.Monitor;

/// <param name="Name">Graphics card name, e.g. "NVIDIA GeForce RTX 4070".</param>
/// <param name="Load">Busiest engine of the card, 0..100 (what Task Manager shows as the GPU percentage).</param>
/// <param name="TempC">Core temperature; only known when the card's tool (nvidia-smi) is installed.</param>
public record GpuStat(string Name, double Load, long MemUsedMb, long MemTotalMb, double? TempC);

/// <summary>
/// Graphics card load and video memory. Load and memory come from the Windows performance counters that Task Manager
/// uses (any vendor: NVIDIA, AMD, Intel). If NVIDIA's nvidia-smi is installed it is used as well, for exact numbers and the
/// temperature. Null when nothing can be read (no GPU counters, older Windows).
/// </summary>
public sealed class GpuMonitor
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMilliseconds(900);

    private readonly object _lock = new();
    private DateTime _lastRead = DateTime.MinValue;
    private GpuStat? _last;

    // Windows counters
    private readonly Dictionary<string, CounterSample> _prev = new();
    private bool _primed;
    private bool _registryRead;
    private string _regName = "";
    private long _regTotalMb;

    // nvidia-smi
    private bool _smiResolved;
    private string? _smiPath;

    public GpuStat? Read()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (now - _lastRead < MaxAge) return _last; // several phones and screens ask at once: read once
            _lastRead = now;
            try { _last = OperatingSystem.IsWindows() ? ReadWindows() : Fake(now); }
            catch { _last = null; }
            return _last;
        }
    }

    // A moving pretend card so the phone screens can be tried out on Linux (--dev).
    private static GpuStat Fake(DateTime now)
    {
        var t = now.TimeOfDay.TotalSeconds;
        return new GpuStat("Dev GPU", Math.Round(40 + 35 * Math.Sin(t / 5), 1), 3000 + (long)(500 * Math.Sin(t / 9)), 8192, Math.Round(62 + 8 * Math.Sin(t / 7), 1));
    }

    [SupportedOSPlatform("windows")]
    private GpuStat? ReadWindows()
    {
        var smi = ReadNvidiaSmi();
        if (smi != null) return smi;

        var load = EngineLoad();
        var used = DedicatedMemoryUsedMb();
        if (!_registryRead) ReadRegistry();
        if (load == null && used == null) return null;
        return new GpuStat(_regName.Length > 0 ? _regName : "GPU", Math.Round(load ?? 0, 1), used ?? 0, _regTotalMb, null);
    }

    // ---- performance counters ----

    [SupportedOSPlatform("windows")]
    private double? EngineLoad()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine")) return null;
            var all = new PerformanceCounterCategory("GPU Engine").ReadCategory();
            if (!all.Contains("Utilization Percentage")) return null;

            // Instances are per process and engine ("pid_12_luid_..._phys_0_eng_3_engtype_3D"); an engine's load is the sum over processes.
            var perEngine = new Dictionary<string, double>();
            var seen = new HashSet<string>();
            foreach (InstanceData d in all["Utilization Percentage"].Values)
            {
                var name = d.InstanceName;
                seen.Add(name);
                if (_prev.TryGetValue(name, out var before))
                {
                    var key = name.Contains("_luid_") ? name[name.IndexOf("_luid_", StringComparison.Ordinal)..] : name;
                    perEngine[key] = perEngine.GetValueOrDefault(key) + CounterSample.Calculate(before, d.Sample);
                }
                _prev[name] = d.Sample;
            }
            foreach (var gone in _prev.Keys.Where(k => !seen.Contains(k)).ToList()) _prev.Remove(gone);

            var wasPrimed = _primed;
            _primed = true;
            if (!wasPrimed) return null; // rates need two samples
            return perEngine.Count == 0 ? 0 : Math.Clamp(perEngine.Values.Max(), 0, 100);
        }
        catch { return null; }
    }

    [SupportedOSPlatform("windows")]
    private static long? DedicatedMemoryUsedMb()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Adapter Memory")) return null;
            var all = new PerformanceCounterCategory("GPU Adapter Memory").ReadCategory();
            if (!all.Contains("Dedicated Usage")) return null;
            long best = 0;
            foreach (InstanceData d in all["Dedicated Usage"].Values) best = Math.Max(best, d.Sample.RawValue); // the card with the most in use (skips a small integrated one)
            return best / 1048576;
        }
        catch { return null; }
    }

    // Name and total video memory of the card with the most memory, from the display adapter registry entries.
    [SupportedOSPlatform("windows")]
    private void ReadRegistry()
    {
        _registryRead = true;
        try
        {
            using var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-911a-08002be2e2f7}");
            if (cls == null) return;
            foreach (var sub in cls.GetSubKeyNames().Where(s => s.Length == 4 && s.All(char.IsAsciiDigit)))
            {
                using var k = cls.OpenSubKey(sub);
                var raw = k?.GetValue("HardwareInformation.qwMemorySize");
                long bytes = raw switch { long l => l, byte[] b when b.Length >= 8 => BitConverter.ToInt64(b, 0), int i => i, _ => 0 };
                var name = k?.GetValue("DriverDesc") as string ?? "";
                var mb = bytes / 1048576;
                if (mb > _regTotalMb || (_regName.Length == 0 && name.Length > 0)) { _regTotalMb = Math.Max(mb, _regTotalMb); if (name.Length > 0) _regName = name; }
            }
        }
        catch { }
    }

    // ---- nvidia-smi (NVIDIA only: exact load/memory and the temperature) ----

    [SupportedOSPlatform("windows")]
    private GpuStat? ReadNvidiaSmi()
    {
        if (!_smiResolved)
        {
            _smiResolved = true;
            var candidates = new List<string>
            {
                Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NVIDIA Corporation\NVSMI\nvidia-smi.exe"),
            };
            candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => Path.Combine(d.Trim(), "nvidia-smi.exe")));
            _smiPath = candidates.FirstOrDefault(File.Exists);
        }
        if (_smiPath == null) return null;
        try
        {
            var psi = new ProcessStartInfo(_smiPath, "--query-gpu=name,utilization.gpu,memory.used,memory.total,temperature.gpu --format=csv,noheader,nounits")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            var line = p.StandardOutput.ReadLine();
            if (!p.WaitForExit(2000)) { try { p.Kill(); } catch { } return null; }
            if (p.ExitCode != 0 || line == null) return null;
            var parts = line.Split(',', StringSplitOptions.TrimEntries); // first card only
            if (parts.Length < 5) return null;
            double D(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
            double? temp = double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : null;
            return new GpuStat(parts[0], D(parts[1]), (long)D(parts[2]), (long)D(parts[3]), temp);
        }
        catch { _smiPath = null; return null; } // can't run it: use the counters from now on
    }
}
