namespace PaperRoute.Devices.Conversion;

/// <summary>Finds Calibre's <c>ebook-convert.exe</c>.</summary>
public static class CalibreLocator
{
    private const string ExeName = "ebook-convert.exe";

    /// <summary>Looks for <c>ebook-convert.exe</c>: the configured path, the standard install folder, then <c>PATH</c>.</summary>
    /// <param name="configured">Path from the settings file, if any.</param>
    /// <param name="programFiles">Program Files folder; the system's when <see langword="null"/>.</param>
    /// <param name="pathVariable">PATH value; the process's when <see langword="null"/>.</param>
    /// <returns>Full path of the converter, or <see langword="null"/> when Calibre is not installed.</returns>
    public static string? Find(string? configured, string? programFiles = null, string? pathVariable = null)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var standard = Path.Combine(programFiles ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Calibre2", ExeName);
        if (File.Exists(standard))
        {
            return standard;
        }

        var path = pathVariable ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(folder => Path.Combine(folder, ExeName))
            .FirstOrDefault(File.Exists);
    }
}
