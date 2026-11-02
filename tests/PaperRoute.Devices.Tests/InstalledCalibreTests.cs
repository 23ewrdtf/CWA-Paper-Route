using PaperRoute.Devices.Conversion;

namespace PaperRoute.Devices.Tests;

public sealed class InstalledCalibreTests : IDisposable
{
    private static readonly string FakeExe = Path.Join(AppContext.BaseDirectory, "PaperRoute.FakeConverter.exe");
    private readonly string _dir = Path.Join(Path.GetTempPath(), "paperroute-inst-" + Guid.NewGuid().ToString("N"));

    public InstalledCalibreTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Calibre_installed_after_start_is_picked_up_without_restart()
    {
        string? installed = null;
        var converter = new InstalledCalibre(() => installed, TimeSpan.FromSeconds(30), _dir);
        Assert.False(converter.IsAvailable);

        installed = FakeExe; // the user installs Calibre while Paper Route keeps running

        Assert.True(converter.IsAvailable);
        Assert.Equal("a.azw3", converter.ToAzw3(new PaperRoute.Delivery.BookAttachment("a.epub", "ok"u8.ToArray())).FileName);
    }

    [Fact]
    public void Uninstalled_calibre_gives_conversion_error() =>
        Assert.Throws<PaperRoute.Delivery.ConversionException>(() =>
            new InstalledCalibre(() => null, TimeSpan.FromSeconds(5), _dir).ToAzw3(new PaperRoute.Delivery.BookAttachment("a.epub", "ok"u8.ToArray())));
}
