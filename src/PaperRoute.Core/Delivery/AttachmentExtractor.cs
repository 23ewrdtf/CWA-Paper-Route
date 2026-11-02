using System.Text;

using MimeKit;

namespace PaperRoute.Delivery;

/// <summary>Takes attachments out of a received message and makes their names safe for Windows.</summary>
public static class AttachmentExtractor
{
    /// <summary>Longest file name produced, including the extension.</summary>
    public const int MaxFileNameLength = 150;

    private const string FallbackName = "book";

    private static readonly HashSet<char> InvalidChars =
        [.. Path.GetInvalidFileNameChars(), ':', '?', '*', '"', '<', '>', '|', '/', '\\'];

    // Windows device names (learn.microsoft.com "Naming Files, Paths, and Namespaces"), reserved with any extension.
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM\u00B9", "COM\u00B2", "COM\u00B3",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT\u00B9", "LPT\u00B2", "LPT\u00B3",
    };

    /// <summary>Returns every MIME part that carries a file name, with the name sanitized.</summary>
    /// <param name="message">The received message.</param>
    /// <returns>The attachments, in message order.</returns>
    public static IReadOnlyList<BookAttachment> Extract(MimeMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var result = new List<BookAttachment>();
        foreach (var part in message.BodyParts.OfType<MimePart>())
        {
            if (string.IsNullOrEmpty(part.FileName) || part.Content is null)
            {
                continue;
            }

            using var buffer = new MemoryStream();
            part.Content.DecodeTo(buffer);
            result.Add(new BookAttachment(SanitizeFileName(part.FileName), buffer.ToArray()));
        }

        return result;
    }

    /// <summary>Makes a file name safe to create on Windows.</summary>
    /// <param name="name">Name as received.</param>
    /// <returns>A non-empty name of at most <see cref="MaxFileNameLength"/> characters.</returns>
    public static string SanitizeFileName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(InvalidChars.Contains(c) || char.IsControl(c) ? '_' : c);
        }

        var cleaned = builder.ToString().TrimEnd('.', ' ');
        if (cleaned.Trim().Length == 0)
        {
            return FallbackName;
        }

        // Windows treats "CON.tar.azw3" as the device CON: check the part before the first dot.
        var firstSegment = cleaned.Split('.', 2)[0].TrimEnd(' ');
        if (ReservedNames.Contains(firstSegment))
        {
            cleaned = "_" + cleaned;
        }

        return Truncate(cleaned);
    }

    private static string Truncate(string name)
    {
        if (name.Length <= MaxFileNameLength)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        if (extension.Length >= MaxFileNameLength / 2)
        {
            extension = string.Empty;
        }

        var cut = MaxFileNameLength - extension.Length;
        if (char.IsHighSurrogate(name[cut - 1]))
        {
            cut--; // never split an emoji or other two-part character
        }

        var stem = name[..cut].TrimEnd('.', ' ');
        return (stem.Length == 0 ? FallbackName : stem) + extension;
    }
}
