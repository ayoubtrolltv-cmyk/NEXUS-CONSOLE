namespace Nexus.Core.Logging;

/// <summary>Journal fichier minimal (un fichier par jour, 7 jours conservés). Ne lève jamais d'exception.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string? _dir;

    public static void Init(string dir)
    {
        _dir = dir;
        try
        {
            Directory.CreateDirectory(dir);
            foreach (var f in new DirectoryInfo(dir).GetFiles("nexus-*.log").OrderByDescending(x => x.Name).Skip(7))
                f.Delete();
        }
        catch { /* le journal ne doit jamais bloquer l'application */ }
    }

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        if (_dir is null) return;
        try
        {
            lock (Gate)
            {
                var file = Path.Combine(_dir, $"nexus-{DateTime.Now:yyyyMMdd}.log");
                var tail = ex is null ? "" : Environment.NewLine + ex;
                File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{tail}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
