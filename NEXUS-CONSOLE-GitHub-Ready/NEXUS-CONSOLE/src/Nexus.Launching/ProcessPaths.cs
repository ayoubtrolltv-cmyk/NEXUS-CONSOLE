using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Nexus.Launching;

/// <summary>Chemin complet de l'exécutable de chaque processus, y compris élevés (droit de lecture limité suffisant).</summary>
internal static class ProcessPaths
{
    private const uint QueryLimited = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder name, ref int size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    public static string? Get(int pid)
    {
        var h = OpenProcess(QueryLimited, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            var size = sb.Capacity;
            return QueryFullProcessImageNameW(h, 0, sb, ref size) ? sb.ToString(0, size) : null;
        }
        finally { CloseHandle(h); }
    }

    public static IEnumerable<(int Pid, string Path)> Enumerate()
    {
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                string? path = null;
                try { path = Get(p.Id); } catch { }
                if (path is not null) yield return (p.Id, path);
            }
        }
    }
}
