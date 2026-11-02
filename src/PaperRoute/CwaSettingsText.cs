using System.Globalization;

using PaperRoute.Config;

namespace PaperRoute.App;

/// <summary>The values to paste into CWA's e-mail settings and user profile.</summary>
internal static class CwaSettingsText
{
    /// <summary>Host name of this PC as seen from a Docker Desktop container.</summary>
    public const string DockerHost = "host.docker.internal";

    /// <summary>Builds the text copied by the Settings window's "Copy CWA settings" button.</summary>
    /// <param name="config">Current settings.</param>
    /// <returns>One "name: value" line per CWA setting.</returns>
    public static string Build(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var port = AppConfig.ParseListen(config.Listen).Port;
        return string.Join(
            Environment.NewLine,
            $"SMTP hostname: {DockerHost}",
            string.Create(CultureInfo.InvariantCulture, $"SMTP port: {port}"),
            "Encryption: None",
            $"SMTP login: {config.AuthLogin}",
            $"SMTP password: {config.AuthPassword}",
            "From e-mail: cwa@localhost",
            $"eReader e-mail (user profile): {config.Recipient}");
    }
}
