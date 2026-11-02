using System.Text;

using PaperRoute.Delivery;

namespace PaperRoute.Core.Tests;

public sealed class BookSignatureTests
{
    [Theory]
    [InlineData("azw3", 60, "BOOKMOBI")]
    [InlineData("azw", 60, "BOOKMOBI")]
    [InlineData("mobi", 60, "BOOKMOBI")]
    [InlineData("prc", 60, "TEXtREAd")]
    [InlineData("mobi", 60, "TEXtREAd")]
    [InlineData("pdf", 0, "%PDF-1.4")]
    [InlineData("pdf", 200, "%PDF-1.7")]
    [InlineData("kfx", 0, "CONT")]
    [InlineData("kfx", 0, "êDRMIONî")]
    [InlineData("txt", 0, "Just some text")]
    [InlineData("epub", 0, "PK\u0003\u0004")]
    public void Real_books_match(string extension, int offset, string marker) =>
        Assert.True(BookSignature.Matches(extension, With(offset, marker)));

    [Theory]
    [InlineData("azw3")]
    [InlineData("mobi")]
    [InlineData("pdf")]
    [InlineData("kfx")]
    [InlineData("epub")]
    public void Zero_filled_files_do_not_match(string extension) =>
        Assert.False(BookSignature.Matches(extension, new byte[4096]));

    [Theory]
    [InlineData("azw3")]
    [InlineData("pdf")]
    [InlineData("txt")]
    [InlineData("whatever")]
    public void Renamed_windows_programs_are_refused(string extension)
    {
        var exe = With(0, "MZ");
        Encoding.ASCII.GetBytes("BOOKMOBI").CopyTo(exe, 60); // even with a forged marker

        Assert.False(BookSignature.Matches(extension, exe));
    }

    [Fact]
    public void Linux_programs_are_refused() =>
        Assert.False(BookSignature.Matches("txt", With(0, "\u007fELF")));

    [Fact]
    public void Text_with_binary_zeros_is_refused()
    {
        var bytes = With(0, "Looks like text");
        bytes[^1] = 0;

        Assert.False(BookSignature.Matches("txt", bytes));
    }

    [Fact]
    public void Text_with_binary_zeros_after_64_KiB_is_refused()
    {
        var bytes = new byte[(64 * 1024) + 100];
        Array.Fill(bytes, (byte)'a');
        bytes[(64 * 1024) + 50] = 0;

        Assert.False(BookSignature.Matches("txt", bytes));
    }

    [Fact]
    public void Pdf_marker_after_first_kilobyte_is_refused() =>
        Assert.False(BookSignature.Matches("pdf", With(2000, "%PDF-1.7")));

    [Fact]
    public void Too_short_mobi_is_refused() =>
        Assert.False(BookSignature.Matches("azw3", Encoding.ASCII.GetBytes("BOOKMOBI")));

    [Fact]
    public void Extension_match_ignores_case() =>
        Assert.True(BookSignature.Matches("AZW3", With(60, "BOOKMOBI")));

    [Fact]
    public void Unknown_extensions_only_refuse_programs() =>
        Assert.True(BookSignature.Matches("fb2", With(0, "<?xml version=\"1.0\"?>")));

    private static byte[] With(int offset, string marker)
    {
        var bytes = new byte[4096];
        Array.Fill(bytes, (byte)' ');
        Encoding.Latin1.GetBytes(marker).CopyTo(bytes, offset);
        return bytes;
    }
}
