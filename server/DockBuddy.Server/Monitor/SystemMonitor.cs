using System.Net.NetworkInformation;
using DockBuddy.Server.Platform;

namespace DockBuddy.Server.Monitor;

public class Stats
{
    public string Host { get; set; } = Environment.MachineName;
    public double Cpu { get; set; }
    public double RamPercent { get; set; }
    public long RamUsedMb { get; set; }
    public long RamTotalMb { get; set; }
    public List<DiskStat> Disks { get; set; } = new();
    public long UptimeSeconds { get; set; }
    public double NetDownKbps { get; set; }
    public double NetUpKbps { get; set; }
    // Null when this PC's graphics card can't be read.
    public GpuStat? Gpu { get; set; }
}

public class DiskStat
{
    public string Name { get; set; } = "";
    public double UsedGb { get; set; }
    public double TotalGb { get; set; }
}

public sealed class SystemMonitor
{
    private readonly object _lock = new();
    private ulong _lastIdle, _lastTotal;
    private long _lastRx, _lastTx;
    private DateTime _lastNetTime = DateTime.UtcNow;
    private double _cpu, _down, _up;
    private readonly GpuMonitor _gpu = new();

    private readonly string _host;

    /// <param name="hostName">Name shown for this PC (the server's configured name); defaults to the machine name.</param>
    public SystemMonitor(string? hostName = null)
    {
        _host = string.IsNullOrWhiteSpace(hostName) ? Environment.MachineName : hostName;
        (_lastRx, _lastTx) = NetTotals();
        Sample();
        _gpu.Read(); // first sample of the rate counters
    }

    private static (long rx, long tx) NetTotals()
    {
        long rx = 0, tx = 0;
        try
        {
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus != OperationalStatus.Up) continue;
                if (n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var s = n.GetIPStatistics();
                rx += s.BytesReceived;
                tx += s.BytesSent;
            }
        }
        catch { }
        return (rx, tx);
    }

    private void Sample()
    {
        if (OperatingSystem.IsWindows() && Win32.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            // Kernel time already includes idle time.
            ulong total = kernel.Value + user.Value;
            ulong dTotal = total - _lastTotal, dIdle = idle.Value - _lastIdle;
            if (_lastTotal != 0 && dTotal > 0)
                _cpu = Math.Clamp(100.0 * (dTotal - dIdle) / dTotal, 0, 100);
            _lastTotal = total;
            _lastIdle = idle.Value;
        }
        else if (OperatingSystem.IsLinux()) SampleLinuxCpu(); // --dev on Linux: real numbers instead of 0%

        var (rx, tx) = NetTotals();
        var now = DateTime.UtcNow;
        var secs = (now - _lastNetTime).TotalSeconds;
        if (secs > 0.2)
        {
            _down = Math.Max(0, (rx - _lastRx) / 1024.0 / secs);
            _up = Math.Max(0, (tx - _lastTx) / 1024.0 / secs);
            _lastRx = rx; _lastTx = tx; _lastNetTime = now;
        }
    }

    // First line of /proc/stat: "cpu user nice system idle iowait irq softirq steal ..."
    private void SampleLinuxCpu()
    {
        try
        {
            var f = File.ReadLines("/proc/stat").First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(8).Select(ulong.Parse).ToArray();
            ulong idle = f[3] + f[4], total = f.Aggregate(0UL, (a, b) => a + b);
            ulong dTotal = total - _lastTotal, dIdle = idle - _lastIdle;
            if (_lastTotal != 0 && dTotal > 0) _cpu = Math.Clamp(100.0 * (dTotal - dIdle) / dTotal, 0, 100);
            _lastTotal = total;
            _lastIdle = idle;
        }
        catch { }
    }

    public Stats Read()
    {
        lock (_lock)
        {
            Sample();
            var s = new Stats { Host = _host, Cpu = Math.Round(_cpu, 1), NetDownKbps = Math.Round(_down, 1), NetUpKbps = Math.Round(_up, 1) };
            s.UptimeSeconds = Environment.TickCount64 / 1000;
            s.Gpu = _gpu.Read();

            if (OperatingSystem.IsWindows())
            {
                var m = new Win32.MEMORYSTATUSEX();
                if (Win32.GlobalMemoryStatusEx(m))
                {
                    s.RamTotalMb = (long)(m.ullTotalPhys / 1048576);
                    s.RamUsedMb = (long)((m.ullTotalPhys - m.ullAvailPhys) / 1048576);
                    s.RamPercent = m.dwMemoryLoad;
                }
            }
            else if (OperatingSystem.IsLinux() && File.Exists("/proc/meminfo"))
            {
                long Kb(string key) => File.ReadLines("/proc/meminfo").Where(l => l.StartsWith(key + ":"))
                    .Select(l => long.Parse(l.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1])).FirstOrDefault();
                s.RamTotalMb = Kb("MemTotal") / 1024;
                s.RamUsedMb = s.RamTotalMb - Kb("MemAvailable") / 1024;
                s.RamPercent = s.RamTotalMb > 0 ? Math.Round(100.0 * s.RamUsedMb / s.RamTotalMb, 1) : 0;
            }
            else
            {
                var info = GC.GetGCMemoryInfo();
                s.RamTotalMb = info.TotalAvailableMemoryBytes / 1048576;
                s.RamUsedMb = Math.Min(s.RamTotalMb, GC.GetTotalMemory(false) / 1048576);
                s.RamPercent = s.RamTotalMb > 0 ? Math.Round(100.0 * s.RamUsedMb / s.RamTotalMb, 1) : 0;
            }

            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
                    s.Disks.Add(new DiskStat
                    {
                        Name = d.Name.TrimEnd('\\', '/'),
                        TotalGb = Math.Round(d.TotalSize / 1073741824.0, 1),
                        UsedGb = Math.Round((d.TotalSize - d.AvailableFreeSpace) / 1073741824.0, 1)
                    });
                }
                catch { }
            }
            return s;
        }
    }
}
