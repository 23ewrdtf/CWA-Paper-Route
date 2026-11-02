using System.Globalization;
using System.Net;
using System.Text.Json.Serialization;

namespace PaperRoute.Config;

/// <summary>Settings stored in <c>%APPDATA%\PaperRoute\config.json</c>.</summary>
/// <param name="Listen">Address and port the SMTP server listens on.</param>
/// <param name="AllowedClients">CIDR ranges allowed to connect; <see langword="null"/> means loopback only.</param>
/// <param name="AuthLogin">SMTP login CWA must use.</param>
/// <param name="AuthPassword">SMTP password CWA must use; generated on first run.</param>
/// <param name="Recipient">The only recipient address accepted.</param>
/// <param name="MaxMessageMB">Largest message accepted, in megabytes.</param>
/// <param name="Notifications">Whether tray notifications are shown.</param>
/// <param name="BookExtensions">File extensions treated as books; <see langword="null"/> means the defaults.</param>
/// <param name="CalibrePath">Path of Calibre's <c>ebook-convert.exe</c>; <see langword="null"/> means look in the usual places.</param>
public sealed record AppConfig(
    string Listen = "127.0.0.1:2525",
    IReadOnlyList<string>? AllowedClients = null,
    string AuthLogin = "cwa",
    string AuthPassword = "",
    string Recipient = "kindle@paperroute.local",
    int MaxMessageMB = 200,
    bool Notifications = true,
    IReadOnlyList<string>? BookExtensions = null,
    string? CalibrePath = null)
{
    /// <summary>Lowest port that may be configured.</summary>
    public const int MinPort = 1024;

    /// <summary>Highest port that may be configured.</summary>
    public const int MaxPort = 65535;

    private static readonly string[] DefaultAllowedClients = ["127.0.0.1/32", "::1/128"];

    private static readonly string[] DefaultBookExtensions = ["azw3", "azw", "mobi", "kfx", "prc", "pdf", "txt", "epub"];

    /// <summary>Gets the allowed client ranges, falling back to loopback only.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> AllowedClientsOrDefault => AllowedClients ?? DefaultAllowedClients;

    /// <summary>Gets the book extensions (lower case, no dot), falling back to the defaults.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> BookExtensionsOrDefault => BookExtensions ?? DefaultBookExtensions;

    /// <summary>Parses an <c>address:port</c> listen value.</summary>
    /// <param name="listen">Value such as <c>127.0.0.1:2525</c> or <c>[::1]:2525</c>.</param>
    /// <returns>The endpoint.</returns>
    /// <exception cref="ConfigException">The value is not an endpoint or the port is out of range.</exception>
    public static IPEndPoint ParseListen(string listen)
    {
        ArgumentNullException.ThrowIfNull(listen);
        if (!IPEndPoint.TryParse(listen, out var endpoint) || !listen.Contains(':', StringComparison.Ordinal))
        {
            throw new ConfigException($"'{listen}' is not a valid listen address - use for example 127.0.0.1:2525");
        }

        if (endpoint.Port is < MinPort or > MaxPort)
        {
            throw new ConfigException(string.Create(CultureInfo.InvariantCulture, $"Port {endpoint.Port} is not allowed - use a port from {MinPort} to {MaxPort}"));
        }

        return endpoint;
    }
}
