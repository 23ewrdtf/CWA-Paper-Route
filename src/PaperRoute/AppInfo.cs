using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PaperRoute.App;

/// <summary>Product facts shown in the UI.</summary>
internal static class AppInfo
{
    public const string Name = "Paper Route";

    [SuppressMessage("Major Code Smell", "S1075:URIs should not be hardcoded", Justification = "The project's public home page, shown in About.")]
    public const string ProjectUrl = "https://github.com/23ewrdtf/CWA-Paper-Route";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "dev";

    public static Icon LoadIcon(string name)
    {
        using var stream = typeof(AppInfo).Assembly.GetManifestResourceStream($"PaperRoute.App.Resources.{name}.ico")
            ?? throw new InvalidOperationException($"Missing icon resource {name}");
        return new Icon(stream);
    }
}
