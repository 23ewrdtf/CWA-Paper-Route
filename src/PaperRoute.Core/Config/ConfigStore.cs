using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PaperRoute.Config;

/// <summary>Loads and saves <see cref="AppConfig"/> as JSON.</summary>
public static class ConfigStore
{
    private const int MinMessageMB = 1;
    private const int MaxMessageMB = 1024;
    private const string RandomTokenAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Name of the settings-file field holding the encrypted password.</summary>
    public const string ProtectedPasswordField = "authPasswordProtected";

    private const string PlainPasswordField = "authPassword";

    /// <summary>Loads the config without encryption (plain <c>authPassword</c>); for tests and tools.</summary>
    /// <param name="path">Path of <c>config.json</c>.</param>
    /// <returns>The validated configuration.</returns>
    /// <exception cref="ConfigException">The file is not valid JSON or holds an invalid setting.</exception>
    public static AppConfig LoadOrCreate(string path) => LoadOrCreate(path, protector: null, out _);

    /// <summary>
    /// Loads the config, creating it with defaults and a new password when missing. With a
    /// <paramref name="protector"/>, the password is stored only encrypted; a plain password from an older
    /// version is encrypted and kept.
    /// </summary>
    /// <param name="path">Path of <c>config.json</c>.</param>
    /// <param name="protector">Encrypts the stored password; <see langword="null"/> stores it in plain text.</param>
    /// <param name="passwordReset">
    /// <see langword="true"/> when the stored password could not be decrypted (other Windows user or PC, damaged
    /// file) and a new one was made: the user must paste it into CWA again.
    /// </param>
    /// <returns>The validated configuration.</returns>
    /// <exception cref="ConfigException">The file is not valid JSON or holds an invalid setting.</exception>
    public static AppConfig LoadOrCreate(string path, ISecretProtector? protector, out bool passwordReset)
    {
        passwordReset = false;
        if (!File.Exists(path))
        {
            var created = new AppConfig(AuthPassword: NewPassword());
            Save(path, created, protector);
            return created;
        }

        JsonObject node;
        AppConfig? config;
        try
        {
            node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new ConfigException($"The settings file {path} is empty");
            var protectedPassword = node[ProtectedPasswordField] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            node.Remove(ProtectedPasswordField);
            config = node.Deserialize<AppConfig>(JsonOptions);
            if (config is not null && protectedPassword is not null)
            {
                (config, passwordReset) = Decrypt(config, protectedPassword, protector);
            }

            if (config is not null)
            {
                // A plain password from an older version (no protected field) is saved encrypted below.
                var needsSave = passwordReset || (protector is not null && protectedPassword is null);
                (config, needsSave) = EnsurePassword(config, needsSave);
                Validate(config, path);
                ValidateListen(config, path);
                if (needsSave)
                {
                    Save(path, config, protector);
                }
            }
        }
        catch (JsonException e)
        {
            throw new ConfigException($"The settings file {path} is not valid JSON: {e.Message}", e);
        }

        return config ?? throw new ConfigException($"The settings file {path} is empty");
    }

    private static (AppConfig Config, bool Reset) Decrypt(AppConfig config, string protectedPassword, ISecretProtector? protector)
    {
        if (protector is null)
        {
            return (config with { AuthPassword = string.Empty }, true);
        }

        try
        {
            return (config with { AuthPassword = protector.Unprotect(protectedPassword) }, false);
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
            return (config with { AuthPassword = string.Empty }, true);
        }
    }

    private static (AppConfig Config, bool NeedsSave) EnsurePassword(AppConfig config, bool needsSave) =>
        string.IsNullOrEmpty(config.AuthPassword)
            ? (config with { AuthPassword = NewPassword() }, true)
            : (config, needsSave);

    private static void ValidateListen(AppConfig config, string path)
    {
        try
        {
            _ = AppConfig.ParseListen(config.Listen);
        }
        catch (ConfigException e)
        {
            throw new ConfigException($"The settings file {path} has an invalid listen value: {e.Message}", e);
        }
    }

    // JSON can set non-nullable properties to null or out of range; report it clearly instead of failing later.
    private static void Validate(AppConfig config, string path)
    {
        string? problem = null;
        if (config.Listen is null)
        {
            problem = "\"listen\" is missing - use for example 127.0.0.1:2525";
        }
        else if (string.IsNullOrWhiteSpace(config.AuthLogin))
        {
            problem = "\"authLogin\" is missing";
        }
        else if (config.Recipient is null || !config.Recipient.Contains('@', StringComparison.Ordinal))
        {
            problem = "\"recipient\" must be an e-mail address such as kindle@paperroute.local";
        }
        else if (config.MaxMessageMB is < MinMessageMB or > MaxMessageMB)
        {
            problem = $"\"maxMessageMB\" must be from {MinMessageMB} to {MaxMessageMB}";
        }

        if (problem is not null)
        {
            throw new ConfigException($"The settings file {path} is invalid: {problem}");
        }
    }

    /// <summary>Saves the config atomically (write to a temporary file, then replace).</summary>
    /// <param name="path">Path of <c>config.json</c>.</param>
    /// <param name="config">Configuration to save.</param>
    /// <param name="protector">Encrypts the password; <see langword="null"/> stores it in plain text.</param>
    public static void Save(string path, AppConfig config, ISecretProtector? protector = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var node = JsonSerializer.SerializeToNode(config, JsonOptions)!.AsObject();
        if (protector is not null)
        {
            node.Remove(PlainPasswordField);
            node[ProtectedPasswordField] = protector.Protect(config.AuthPassword);
        }

        var temp = path + ".tmp";
        File.WriteAllText(temp, node.ToJsonString(JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Generates a 32-character alphanumeric password from a cryptographic random source.</summary>
    /// <returns>The new password.</returns>
    public static string NewPassword() => RandomNumberGenerator.GetString(RandomTokenAlphabet, 32);
}
