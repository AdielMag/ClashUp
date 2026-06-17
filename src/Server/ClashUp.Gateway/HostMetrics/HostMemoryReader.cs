namespace ClashUp.Server.Gateway.HostMetrics;

/// <summary>
/// Reads the host VM's memory utilization from <c>/proc/meminfo</c>. On
/// Container-Optimized OS the host's <c>/proc</c> is mounted into the gateway at
/// <c>/host/proc</c> so we report the VM's pressure (across all version
/// backends), not just this container's cgroup.
/// </summary>
public static class HostMemoryReader
{
    private static readonly string[] MemInfoPaths = { "/host/proc/meminfo", "/proc/meminfo" };

    /// <summary>Host memory utilization as a percentage (0-100), or null if unavailable.</summary>
    public static double? ReadUtilizationPercent()
    {
        foreach (var path in MemInfoPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                long total = 0;
                long available = 0;
                foreach (var line in File.ReadLines(path))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    {
                        total = ParseKilobytes(line);
                    }
                    else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                    {
                        available = ParseKilobytes(line);
                    }

                    if (total > 0 && available > 0)
                    {
                        break;
                    }
                }

                if (total > 0)
                {
                    return Math.Clamp((double)(total - available) / total * 100.0, 0, 100);
                }
            }
            catch
            {
                // Try the next path / give up — memory reporting is best-effort.
            }
        }

        return null;
    }

    private static long ParseKilobytes(string line)
    {
        // e.g. "MemTotal:       16331756 kB"
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && long.TryParse(parts[1], out var kb) ? kb : 0;
    }
}
