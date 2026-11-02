namespace PaperRoute.Delivery;

/// <summary>Checks that a file's content matches its book type, so disguised files are refused.</summary>
public static class BookSignature
{
    private const int MobiTypeOffset = 60;
    private const int PdfSearchWindow = 1024;

    private static ReadOnlySpan<byte> BookMobi => "BOOKMOBI"u8;

    private static ReadOnlySpan<byte> TextRead => "TEXtREAd"u8;

    private static ReadOnlySpan<byte> PdfHeader => "%PDF-"u8;

    private static ReadOnlySpan<byte> KfxContainer => "CONT"u8;

    // KFX books with Kindle DRM start with 0xEA "DRMION" 0xEE.
    private static ReadOnlySpan<byte> KfxDrmion => [0xEA, (byte)'D', (byte)'R', (byte)'M', (byte)'I', (byte)'O', (byte)'N', 0xEE];

    private static ReadOnlySpan<byte> ZipArchive => [(byte)'P', (byte)'K', 3, 4];

    private static ReadOnlySpan<byte> WindowsProgram => "MZ"u8;

    private static ReadOnlySpan<byte> LinuxProgram => [0x7F, (byte)'E', (byte)'L', (byte)'F'];

    /// <summary>Returns whether <paramref name="content"/> looks like a real file of type <paramref name="extension"/>.</summary>
    /// <param name="extension">Extension without the dot, any case.</param>
    /// <param name="content">File contents.</param>
    /// <returns><see langword="false"/> for programs and for content that does not match the type.</returns>
    public static bool Matches(string extension, ReadOnlySpan<byte> content)
    {
        ArgumentNullException.ThrowIfNull(extension);
        if (content.StartsWith(WindowsProgram) || content.StartsWith(LinuxProgram))
        {
            return false;
        }

        return extension.ToUpperInvariant() switch
        {
            "AZW3" or "AZW" or "MOBI" or "PRC" => IsMobi(content),
            "PDF" => content[..Math.Min(content.Length, PdfSearchWindow)].IndexOf(PdfHeader) >= 0,
            "KFX" => content.StartsWith(KfxContainer) || content.StartsWith(KfxDrmion),
            "TXT" => !content.Contains((byte)0),
            "EPUB" => content.StartsWith(ZipArchive),
            _ => true, // Types added by the user in config.json: only programs are refused.
        };
    }

    private static bool IsMobi(ReadOnlySpan<byte> content)
    {
        if (content.Length < MobiTypeOffset + BookMobi.Length)
        {
            return false;
        }

        var type = content.Slice(MobiTypeOffset, BookMobi.Length);
        return type.SequenceEqual(BookMobi) || type.SequenceEqual(TextRead);
    }
}
