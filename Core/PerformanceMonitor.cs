using System.Diagnostics;

namespace ScreenZoomUtility.Core;

public sealed class PerformanceMonitor : IDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    private DateTime _lastCheckUtc = DateTime.UtcNow;
    private TimeSpan _lastTotalCpu = TimeSpan.Zero;

    public double CpuPercent { get; private set; }
    public double MemoryMb { get; private set; }

    public PerformanceMonitor()
    {
        _lastTotalCpu = _process.TotalProcessorTime;
    }

    public void Sample()
    {
        _process.Refresh();
        var now = DateTime.UtcNow;
        var currentCpu = _process.TotalProcessorTime;
        var elapsedMs = (now - _lastCheckUtc).TotalMilliseconds;

        if (elapsedMs > 50)
        {
            var cpuUsedMs = (currentCpu - _lastTotalCpu).TotalMilliseconds;
            CpuPercent = Math.Clamp(cpuUsedMs / (Environment.ProcessorCount * elapsedMs) * 100.0, 0, 100);
            _lastCheckUtc = now;
            _lastTotalCpu = currentCpu;
        }

        MemoryMb = _process.WorkingSet64 / (1024.0 * 1024.0);
    }

    public void Dispose()
    {
        _process.Dispose();
    }
}
