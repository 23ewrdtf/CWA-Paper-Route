using Microsoft.Win32;

using PaperRoute.App;

namespace PaperRoute.App.Tests;

public sealed class AutostartTests : IDisposable
{
    private readonly string _keyPath = @"Software\PaperRouteTests\" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey _run;

    public AutostartTests() => _run = Registry.CurrentUser.CreateSubKey(_keyPath);

    public void Dispose()
    {
        _run.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
    }

    [Fact]
    public void Set_true_then_IsEnabled_and_value_is_quoted_exe_path()
    {
        Autostart.Set(_run, enabled: true, @"C:\Apps\Paper Route\PaperRoute.exe");

        Assert.True(Autostart.IsEnabled(_run, @"C:\Apps\Paper Route\PaperRoute.exe"));
        Assert.Equal("\"C:\\Apps\\Paper Route\\PaperRoute.exe\"", _run.GetValue("PaperRoute"));
    }

    [Fact]
    public void Set_false_removes_value()
    {
        Autostart.Set(_run, enabled: true, @"C:\PaperRoute.exe");

        Autostart.Set(_run, enabled: false, @"C:\PaperRoute.exe");

        Assert.False(Autostart.IsEnabled(_run, @"C:\PaperRoute.exe"));
        Assert.Null(_run.GetValue("PaperRoute"));
    }

    [Fact]
    public void Disabled_by_default() => Assert.False(Autostart.IsEnabled(_run, @"C:\PaperRoute.exe"));

    [Fact]
    public void Entry_for_another_copy_counts_as_disabled()
    {
        Autostart.Set(_run, enabled: true, @"C:\Old\PaperRoute.exe");

        Assert.False(Autostart.IsEnabled(_run, @"C:\New\PaperRoute.exe"));
    }
}
