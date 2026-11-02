using FsCheck;
using FsCheck.Xunit;

using MimeKit;

using PaperRoute.Core.Tests.Fakes;
using PaperRoute.Delivery;

namespace PaperRoute.Core.Tests;

public sealed class AttachmentExtractorTests
{
    // Exactly what Python's email package (used by CWA) emits for a non-ASCII attachment name.
    private const string PythonRfc2231Message = """
        From: cwa@localhost
        To: kindle@paperroute.local
        Subject: Send to eReader
        MIME-Version: 1.0
        Content-Type: multipart/mixed; boundary="===============123=="

        --===============123==
        Content-Type: text/plain; charset="utf-8"
        Content-Transfer-Encoding: 7bit

        This e-mail has been sent via Calibre-Web.
        --===============123==
        Content-Type: application/octet-stream
        Content-Transfer-Encoding: base64
        Content-Disposition: attachment; filename*=utf-8''Zagubiona%20przysz%C5%82o%C5%9B%C4%87.azw3

        AAECAw==
        --===============123==--
        """;

    [Fact]
    public void Decodes_rfc2231_polish_name_as_sent_by_python()
    {
        var attachment = AttachmentExtractor.Extract(TestMessages.Parse(PythonRfc2231Message)).Single();

        Assert.Equal("Zagubiona przyszłość.azw3", attachment.FileName);
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, attachment.Content.ToArray());
        Assert.Equal("azw3", attachment.Extension);
    }

    [Fact]
    public void Replaces_illegal_windows_chars() =>
        Assert.Equal("Dune_ Part 1_.azw3", AttachmentExtractor.Extract(TestMessages.Book("Dune: Part 1?.azw3")).Single().FileName);

    [Fact]
    public void Plain_text_body_is_not_an_attachment() =>
        Assert.Empty(AttachmentExtractor.Extract(TestMessages.BodyOnly()));

    [Theory]
    [InlineData("CON.azw3", "_CON.azw3")]
    [InlineData("nul", "_nul")]
    [InlineData("com1.mobi", "_com1.mobi")]
    [InlineData("LPT9.pdf", "_LPT9.pdf")]
    [InlineData("CONSOLE.azw3", "CONSOLE.azw3")]
    [InlineData("CON.tar.azw3", "_CON.tar.azw3")]
    [InlineData("aux .backup.mobi", "_aux .backup.mobi")]
    [InlineData("CONIN$.azw3", "_CONIN$.azw3")]
    [InlineData("conout$", "_conout$")]
    [InlineData("COM\u00B9.azw3", "_COM\u00B9.azw3")]
    [InlineData("LPT\u00B3.pdf", "_LPT\u00B3.pdf")]
    [InlineData("COM0.azw3", "_COM0.azw3")]
    public void Reserved_device_names_are_prefixed(string name, string expected) =>
        Assert.Equal(expected, AttachmentExtractor.SanitizeFileName(name));

    [Fact]
    public void Path_traversal_is_flattened() =>
        Assert.Equal(".._.._evil.azw3", AttachmentExtractor.SanitizeFileName("../../evil.azw3"));

    [Fact]
    public void Backslash_paths_are_flattened() =>
        Assert.Equal("C__Windows_evil.azw3", AttachmentExtractor.SanitizeFileName("C:\\Windows\\evil.azw3"));

    [Theory]
    [InlineData("book.azw3. . ", "book.azw3")]
    [InlineData("   ", "book")]
    [InlineData("...", "book")]
    public void Trailing_dots_and_spaces_are_trimmed(string name, string expected) =>
        Assert.Equal(expected, AttachmentExtractor.SanitizeFileName(name));

    [Fact]
    public void Long_names_are_cut_to_150_chars_keeping_extension()
    {
        var name = AttachmentExtractor.SanitizeFileName(new string('a', 300) + ".azw3");

        Assert.Equal(150, name.Length);
        Assert.EndsWith(".azw3", name, StringComparison.Ordinal);
    }

    [Fact]
    public void Truncation_never_splits_an_emoji()
    {
        // 145 letters + an emoji (2 UTF-16 units) across the cut point + more text.
        var name = AttachmentExtractor.SanitizeFileName(new string('a', 144) + "\U0001F4DA" + "bbbbbbbbbb.azw3");

        Assert.True(name.Length <= AttachmentExtractor.MaxFileNameLength);
        for (var i = 0; i < name.Length; i++)
        {
            Assert.False(char.IsHighSurrogate(name[i]) && (i + 1 == name.Length || !char.IsLowSurrogate(name[i + 1])), $"lone surrogate at {i}");
        }
    }

    [Property]
    public bool Sanitized_names_are_always_valid_windows_file_names(NonEmptyString name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var sanitized = AttachmentExtractor.SanitizeFileName(name.Get);
        return sanitized.Length is > 0 and <= 150
            && sanitized.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && sanitized.IndexOfAny([':', '?', '*', '"', '<', '>', '|']) < 0
            && !sanitized.EndsWith('.')
            && !sanitized.EndsWith(' ');
    }

    [Property]
    public void Garbage_input_never_throws(byte[] junk)
    {
        MimeMessage message;
        try
        {
            using var stream = new MemoryStream(junk);
            message = MimeMessage.Load(stream);
        }
        catch (FormatException)
        {
            return; // MimeKit's ParseException derives from FormatException: rejecting junk is fine.
        }

        _ = AttachmentExtractor.Extract(message);
    }
}
