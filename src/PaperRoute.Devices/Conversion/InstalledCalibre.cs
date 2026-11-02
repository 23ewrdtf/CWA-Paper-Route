using PaperRoute.Delivery;

namespace PaperRoute.Devices.Conversion;

/// <summary>Converts with whatever Calibre is installed right now, so installing it needs no restart.</summary>
/// <param name="find">Finds <c>ebook-convert.exe</c>; returns <see langword="null"/> when Calibre is not installed.</param>
/// <param name="timeout">Longest a conversion may take.</param>
/// <param name="tempRoot">Folder for temporary files; the user's temp folder when <see langword="null"/>.</param>
public sealed class InstalledCalibre(Func<string?> find, TimeSpan timeout, string? tempRoot = null) : IBookConverter
{
    /// <inheritdoc/>
    public bool IsAvailable => find() is { } path && File.Exists(path);

    /// <inheritdoc/>
    public BookAttachment ToAzw3(BookAttachment epub)
    {
        var exe = find() ?? throw new ConversionException("Calibre is not installed");
        return new CalibreConverter(exe, timeout, tempRoot).ToAzw3(epub);
    }
}
