using System.Diagnostics.CodeAnalysis;

namespace PaperRoute.Delivery;

/// <summary>An attachment taken from a received message.</summary>
/// <param name="FileName">Sanitized file name.</param>
/// <param name="Content">File contents.</param>
public sealed record BookAttachment(string FileName, ReadOnlyMemory<byte> Content)
{
    /// <summary>Gets the extension in lower case without the dot, or an empty string.</summary>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "File extensions are shown and configured in lower case; only compared case-insensitively.")]
    public string Extension => Path.GetExtension(FileName).TrimStart('.').ToLowerInvariant();
}
