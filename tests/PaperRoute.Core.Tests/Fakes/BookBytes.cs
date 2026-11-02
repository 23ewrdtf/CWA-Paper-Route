using System.Text;

namespace PaperRoute.Core.Tests.Fakes;

/// <summary>Minimal byte patterns that pass Paper Route's file-type check.</summary>
internal static class BookBytes
{
    public static byte[] For(string fileName, int size)
    {
        var bytes = new byte[Math.Max(size, 80)];
        var marker = Path.GetExtension(fileName).ToUpperInvariant() switch
        {
            ".AZW3" or ".AZW" or ".MOBI" or ".PRC" => (Offset: 60, Text: "BOOKMOBI"),
            ".PDF" => (Offset: 0, Text: "%PDF-1.7"),
            ".KFX" => (Offset: 0, Text: "CONT"),
            ".TXT" => (Offset: 0, Text: "Plain text book"),
            ".EPUB" => (Offset: 0, Text: "PK\u0003\u0004mimetypeapplication/epub+zip"),
            _ => (Offset: 0, Text: string.Empty),
        };
        if (marker.Text.Length > 0 && Path.GetExtension(fileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            Array.Fill(bytes, (byte)'a');
        }

        Encoding.ASCII.GetBytes(marker.Text).CopyTo(bytes, marker.Offset);
        return bytes;
    }
}
