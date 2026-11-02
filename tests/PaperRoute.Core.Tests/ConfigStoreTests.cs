using PaperRoute.Config;

namespace PaperRoute.Core.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "paperroute-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public ConfigStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "config.json");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Missing_file_writes_and_returns_defaults()
    {
        var config = ConfigStore.LoadOrCreate(_path);

        Assert.Equal("127.0.0.1:2525", config.Listen);
        Assert.Equal(200, config.MaxMessageMB);
        Assert.Equal("cwa", config.AuthLogin);
        Assert.Equal("kindle@paperroute.local", config.Recipient);
        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void Invalid_json_throws_ConfigException_naming_file()
    {
        File.WriteAllText(_path, "{");

        var error = Assert.Throws<ConfigException>(() => ConfigStore.LoadOrCreate(_path));

        Assert.Contains(_path, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void First_run_generates_32_char_alphanumeric_password()
    {
        var config = ConfigStore.LoadOrCreate(_path);

        Assert.Matches("^[A-Za-z0-9]{32}$", config.AuthPassword);
        Assert.NotEqual(config.AuthPassword, ConfigStore.NewPassword(), StringComparer.Ordinal);
    }

    [Fact]
    public void Password_survives_reload()
    {
        var first = ConfigStore.LoadOrCreate(_path);

        Assert.Equal(first.AuthPassword, ConfigStore.LoadOrCreate(_path).AuthPassword);
    }

    [Fact]
    public void Empty_password_in_file_is_generated_and_saved()
    {
        ConfigStore.Save(_path, new AppConfig(AuthPassword: ""));

        var loaded = ConfigStore.LoadOrCreate(_path);

        Assert.Matches("^[A-Za-z0-9]{32}$", loaded.AuthPassword);
        Assert.Equal(loaded.AuthPassword, ConfigStore.LoadOrCreate(_path).AuthPassword);
    }

    [Theory]
    [InlineData("127.0.0.1:80")]
    [InlineData("127.0.0.1:70000")]
    [InlineData("not-an-endpoint")]
    public void Bad_listen_value_is_ConfigException(string listen)
    {
        ConfigStore.Save(_path, new AppConfig(Listen: listen, AuthPassword: "x"));

        Assert.Throws<ConfigException>(() => ConfigStore.LoadOrCreate(_path));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1025)]
    public void Out_of_range_message_size_is_ConfigException(int megabytes)
    {
        ConfigStore.Save(_path, new AppConfig(AuthPassword: "x", MaxMessageMB: megabytes));

        var error = Assert.Throws<ConfigException>(() => ConfigStore.LoadOrCreate(_path));

        Assert.Contains("maxMessageMB", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("listen")]
    [InlineData("authLogin")]
    [InlineData("recipient")]
    public void Missing_required_value_is_ConfigException(string field)
    {
        File.WriteAllText(_path, "{ \"" + field + "\": null, \"authPassword\": \"x\" }");

        var error = Assert.Throws<ConfigException>(() => ConfigStore.LoadOrCreate(_path));

        Assert.Contains(field, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Null_password_is_generated()
    {
        File.WriteAllText(_path, "{ \"authPassword\": null }");

        Assert.Matches("^[A-Za-z0-9]{32}$", ConfigStore.LoadOrCreate(_path).AuthPassword);
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var config = new AppConfig(Listen: "127.0.0.1:2626", AuthPassword: "secret", MaxMessageMB: 50);

        ConfigStore.Save(_path, config);

        Assert.Equal(config with { }, ConfigStore.LoadOrCreate(_path), AppConfigComparer.Instance);
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public void ParseListen_returns_endpoint()
    {
        var endpoint = AppConfig.ParseListen("127.0.0.1:2626");

        Assert.Equal("127.0.0.1", endpoint.Address.ToString());
        Assert.Equal(2626, endpoint.Port);
    }

    private sealed class AppConfigComparer : IEqualityComparer<AppConfig>
    {
        public static readonly AppConfigComparer Instance = new();

        public bool Equals(AppConfig? x, AppConfig? y) =>
            x is not null && y is not null
            && string.Equals(x.Listen, y.Listen, StringComparison.Ordinal)
            && string.Equals(x.AuthLogin, y.AuthLogin, StringComparison.Ordinal)
            && string.Equals(x.AuthPassword, y.AuthPassword, StringComparison.Ordinal)
            && string.Equals(x.Recipient, y.Recipient, StringComparison.Ordinal)
            && x.MaxMessageMB == y.MaxMessageMB && x.Notifications == y.Notifications
            && x.AllowedClientsOrDefault.SequenceEqual(y.AllowedClientsOrDefault, StringComparer.Ordinal)
            && x.BookExtensionsOrDefault.SequenceEqual(y.BookExtensionsOrDefault, StringComparer.Ordinal);

        public int GetHashCode(AppConfig obj) => obj.Listen.GetHashCode(StringComparison.Ordinal);
    }
}
