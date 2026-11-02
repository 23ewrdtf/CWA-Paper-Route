using Microsoft.Win32;

namespace PaperRoute.App;

/// <summary>"Start with Windows" via the per-user Run key (no admin rights needed).</summary>
internal static class Autostart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PaperRoute";

    /// <summary>Returns whether this copy of Paper Route starts with Windows.</summary>
    /// <returns><see langword="true"/> when the Run entry points to this executable.</returns>
    public static bool IsEnabled()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return run is not null && IsEnabled(run);
    }

    /// <summary>Turns "Start with Windows" on or off for this executable.</summary>
    /// <param name="enabled">Whether Paper Route should start with Windows.</param>
    public static void Set(bool enabled)
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        Set(run, enabled, CurrentExe());
    }

    internal static bool IsEnabled(RegistryKey run, string? exePath = null) =>
        run.GetValue(ValueName) is string value
        && string.Equals(value, Quote(exePath ?? CurrentExe()), StringComparison.OrdinalIgnoreCase);

    internal static void Set(RegistryKey run, bool enabled, string exePath)
    {
        if (enabled)
        {
            run.SetValue(ValueName, Quote(exePath), RegistryValueKind.String);
        }
        else
        {
            run.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string CurrentExe() => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot find Paper Route's own executable");

    private static string Quote(string path) => "\"" + path + "\"";
}
