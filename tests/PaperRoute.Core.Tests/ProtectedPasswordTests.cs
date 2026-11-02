using System.Text.Json.Nodes;

using PaperRoute.Config;
using PaperRoute.Core.Tests.Fakes;

namespace PaperRoute.Core.Tests;

public sealed class ProtectedPasswordTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "paperroute-secret-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public ProtectedPasswordTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "config.json");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void New_config_stores_only_the_protected_password()
    {
        var config = ConfigStore.LoadOrCreate(_path, new FakeProtector(), out var reset);

        var json = JsonNode.Parse(File.ReadAllText(_path))!.AsObject();
        Assert.False(json.ContainsKey("authPassword"));
        Assert.StartsWith("FAKE:", json["authPasswordProtected"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(config.AuthPassword, File.ReadAllText(_path), StringComparison.Ordinal);
        Assert.False(reset);
    }

    [Fact]
    public void Protected_password_reads_back()
    {
        var first = ConfigStore.LoadOrCreate(_path, new FakeProtector(), out _);

        var second = ConfigStore.LoadOrCreate(_path, new FakeProtector(), out var reset);

        Assert.Equal(first.AuthPassword, second.AuthPassword);
        Assert.False(reset);
    }

    [Fact]
    public void Plain_password_from_older_versions_is_migrated_and_kept()
    {
        ConfigStore.Save(_path, new AppConfig(AuthPassword: "OldPlainPassword1234"));

        var config = ConfigStore.LoadOrCreate(_path, new FakeProtector(), out var reset);

        Assert.Equal("OldPlainPassword1234", config.AuthPassword); // CWA keeps working
        Assert.False(reset);
        var json = JsonNode.Parse(File.ReadAllText(_path))!.AsObject();
        Assert.False(json.ContainsKey("authPassword"));
        Assert.True(json.ContainsKey("authPasswordProtected"));
    }

    [Fact]
    public void Unreadable_protected_password_is_replaced_and_reported()
    {
        _ = ConfigStore.LoadOrCreate(_path, new FakeProtector(), out _);

        var config = ConfigStore.LoadOrCreate(_path, new FakeProtector { FailToUnprotect = true }, out var reset);

        Assert.True(reset);
        Assert.Matches("^[A-Za-z0-9]{32}$", config.AuthPassword);
    }

    [Fact]
    public void Save_with_protector_never_writes_the_plain_password()
    {
        ConfigStore.Save(_path, new AppConfig(AuthPassword: "SecretPassword1234"), new FakeProtector());

        Assert.DoesNotContain("SecretPassword1234", File.ReadAllText(_path), StringComparison.Ordinal);
        Assert.Equal("SecretPassword1234", ConfigStore.LoadOrCreate(_path, new FakeProtector(), out _).AuthPassword);
    }
}
