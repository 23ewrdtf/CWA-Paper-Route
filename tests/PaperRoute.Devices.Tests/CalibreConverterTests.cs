using System.IO.Compression;
using System.Text;

using PaperRoute.Delivery;
using PaperRoute.Devices.Conversion;

namespace PaperRoute.Devices.Tests;

public sealed class CalibreConverterTests : IDisposable
{
    private static readonly string FakeExe = Path.Combine(AppContext.BaseDirectory, "PaperRoute.FakeConverter.exe");
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "paperroute-conv-" + Guid.NewGuid().ToString("N"));

    public CalibreConverterTests() => Directory.CreateDirectory(_temp);

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void Converts_and_renames_to_azw3()
    {
        var result = Converter().ToAzw3(Epub("Dune: Part 1.epub", "any epub"));

        Assert.Equal("Dune: Part 1.azw3", result.FileName);
        Assert.True(BookSignature.Matches("azw3", result.Content.Span));
    }

    [Fact]
    public void Failure_reports_calibres_error()
    {
        var error = Assert.Throws<ConversionException>(() => Converter().ToAzw3(Epub("a.epub", "MODE:fail")));

        Assert.Contains("this book is broken", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Leftover_helper_process_holding_the_output_does_not_hang_the_conversion()
    {
        var started = DateTime.UtcNow;

        var result = Converter().ToAzw3(Epub("a.epub", "MODE:orphan"));

        Assert.Equal("a.azw3", result.FileName);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(30), "conversion waited for the helper process");
        Assert.Empty(Directory.GetFileSystemEntries(_temp)); // helper stopped, so the private folder could be deleted
    }

    [Fact]
    public void Failing_conversion_with_leftover_helper_does_not_hang()
    {
        var started = DateTime.UtcNow;

        Assert.Throws<ConversionException>(() => Converter().ToAzw3(Epub("a.epub", "MODE:orphan MODE:fail")));

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(30), "waited for the helper process");
        Assert.Empty(Directory.GetFileSystemEntries(_temp));
    }

    [Fact]
    public void Missing_output_is_an_error() =>
        Assert.Throws<ConversionException>(() => Converter().ToAzw3(Epub("a.epub", "MODE:noout")));

    [Fact]
    public void Hanging_conversion_is_stopped()
    {
        var started = DateTime.UtcNow;

        var error = Assert.Throws<ConversionException>(() => Converter(TimeSpan.FromSeconds(2)).ToAzw3(Epub("a.epub", "MODE:hang")));

        Assert.Contains("took longer than", error.Message, StringComparison.Ordinal);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Temporary_files_are_removed()
    {
        var converter = Converter();

        converter.ToAzw3(Epub("a.epub", "ok"));
        Assert.Throws<ConversionException>(() => converter.ToAzw3(Epub("b.epub", "MODE:fail")));

        Assert.Empty(Directory.GetFiles(_temp, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Calibre_scratch_files_stay_in_the_private_folder_and_are_removed()
    {
        var result = Converter().ToAzw3(Epub("a.epub", "MODE:env"));

        Assert.Equal("a.azw3", result.FileName);
        Assert.Empty(Directory.GetFiles(_temp, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Missing_executable_is_unavailable_and_errors()
    {
        var converter = new CalibreConverter(Path.Combine(_temp, "nope.exe"), TimeSpan.FromSeconds(5), _temp);

        Assert.False(converter.IsAvailable);
        Assert.Throws<ConversionException>(() => converter.ToAzw3(Epub("a.epub", "ok")));
    }

    [Fact]
    public void Real_calibre_converts_a_small_epub()
    {
        var calibre = CalibreLocator.Find(configured: null);
        Assert.SkipWhen(calibre is null, "Calibre is not installed on this machine");

        var result = new CalibreConverter(calibre, TimeSpan.FromMinutes(5), _temp).ToAzw3(new BookAttachment("Test book.epub", MinimalEpub()));

        Assert.Equal("Test book.azw3", result.FileName);
        Assert.True(BookSignature.Matches("azw3", result.Content.Span));
    }

    private static BookAttachment Epub(string name, string text) => new(name, Encoding.ASCII.GetBytes(text));

    private static byte[] MinimalEpub()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "mimetype", "application/epub+zip", CompressionLevel.NoCompression);
            Add(zip, "META-INF/container.xml", """<?xml version="1.0"?><container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container"><rootfiles><rootfile full-path="content.opf" media-type="application/oebps-package+xml"/></rootfiles></container>""");
            Add(zip, "content.opf", """<?xml version="1.0"?><package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="id"><metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>Test book</dc:title><dc:language>en</dc:language><dc:identifier id="id">paperroute-test</dc:identifier></metadata><manifest><item id="c1" href="c1.xhtml" media-type="application/xhtml+xml"/></manifest><spine><itemref idref="c1"/></spine></package>""");
            Add(zip, "c1.xhtml", """<?xml version="1.0"?><html xmlns="http://www.w3.org/1999/xhtml"><head><title>Test</title></head><body><p>Hello from Paper Route.</p></body></html>""");
        }

        return buffer.ToArray();
    }

    private static void Add(ZipArchive zip, string name, string text, CompressionLevel level = CompressionLevel.Optimal)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, level).Open());
        writer.Write(text);
    }

    private CalibreConverter Converter(TimeSpan? timeout = null) => new(FakeExe, timeout ?? TimeSpan.FromSeconds(30), _temp);
}
