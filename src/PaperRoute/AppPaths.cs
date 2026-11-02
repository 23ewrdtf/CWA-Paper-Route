namespace PaperRoute.App;

/// <summary>Where Paper Route keeps its settings and logs.</summary>
/// <param name="ConfigFile">Path of <c>config.json</c>.</param>
/// <param name="LogDirectory">Folder for the daily activity logs.</param>
internal sealed record AppPaths(string ConfigFile, string LogDirectory)
{
    /// <summary>Gets the per-user default locations.</summary>
    public static AppPaths Default { get; } = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PaperRoute", "config.json"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaperRoute", "logs"));
}
