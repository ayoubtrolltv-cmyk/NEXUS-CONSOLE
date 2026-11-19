using Microsoft.Win32;
using Nexus.Core.Logging;

namespace Nexus.Shell.Services;

/// <summary>Démarrage avec Windows via la clé Run de l'utilisateur : aucun droit administrateur, aucun service.</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NexusConsole";

    public static bool IsEnabled()
    {
        try { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue(ValueName) is not null; }
        catch { return false; }
    }

    public static bool Set(bool enabled)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (exe is null) return false;
                k.SetValue(ValueName, $"\"{exe}\"");
            }
            else k.DeleteValue(ValueName, false);
            return true;
        }
        catch (Exception ex) { Log.Warn("Démarrage automatique non modifiable.", ex); return false; }
    }
}
