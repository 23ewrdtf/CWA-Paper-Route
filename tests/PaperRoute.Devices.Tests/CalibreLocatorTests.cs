using PaperRoute.Devices.Conversion;

namespace PaperRoute.Devices.Tests;

public sealed class CalibreLocatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "paperroute-loc-" + Guid.NewGuid().ToString("N"));

    public CalibreLocatorTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Configured_path_wins()
    {
        var configured = Touch("custom", "ebook-convert.exe");
        Touch("pf", "Calibre2", "ebook-convert.exe");

        Assert.Equal(configured, CalibreLocator.Find(configured, Path.Combine(_dir, "pf"), pathVariable: string.Empty));
    }

    [Fact]
    public void Standard_install_folder_is_used()
    {
        var standard = Touch("pf", "Calibre2", "ebook-convert.exe");

        Assert.Equal(standard, CalibreLocator.Find(null, Path.Combine(_dir, "pf"), pathVariable: string.Empty));
    }

    [Fact]
    public void Path_variable_is_searched_last()
    {
        var onPath = Touch("bin", "ebook-convert.exe");

        Assert.Equal(onPath, CalibreLocator.Find(null, Path.Combine(_dir, "none"), pathVariable: Path.Combine(_dir, "missing") + ";" + Path.Combine(_dir, "bin")));
    }

    [Fact]
    public void Missing_configured_path_falls_back() =>
        Assert.Equal(Touch("pf", "Calibre2", "ebook-convert.exe"), CalibreLocator.Find(Path.Combine(_dir, "gone.exe"), Path.Combine(_dir, "pf"), pathVariable: string.Empty));

    [Fact]
    public void Nothing_found_is_null() =>
        Assert.Null(CalibreLocator.Find(null, Path.Combine(_dir, "none"), pathVariable: string.Empty));

    private string Touch(params string[] parts)
    {
        var path = Path.Combine([_dir, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }
}
