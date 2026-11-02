using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using PaperRoute.Delivery;

namespace PaperRoute.Devices.MassStorage;

/// <summary>
/// Writes the cover thumbnail a Kindle shows in its library, like Calibre's Kindle driver does. Without it
/// a copied "EBOK" book waits for a cover from Amazon (and Kindles on WiFi blank such covers).
/// </summary>
internal static partial class CoverThumbnail
{
    /// <summary>Largest thumbnail width, measured on Kindle-made thumbnails (Paperwhite).</summary>
    public const int MaxWidth = 214;

    /// <summary>Largest thumbnail height, measured on Kindle-made thumbnails (Paperwhite).</summary>
    public const int MaxHeight = 330;

    private const long JpegQuality = 90L;

    /// <summary>Best effort: writes the thumbnail and returns whether it did; never throws for bad input or I/O.</summary>
    /// <param name="thumbnails">The Kindle's <c>system\thumbnails</c> folder (from the drive Windows reported).</param>
    /// <param name="book">The book that was just copied.</param>
    /// <returns><see langword="true"/> when a thumbnail was written.</returns>
    public static bool TryWrite(DirectoryInfo thumbnails, ReadOnlySpan<byte> book)
    {
        var info = KindleBookInfo.TryRead(book);
        if (info is not { Asin: { } asin, CdeType: { } type, Cover: { } cover } || !IsSafe(asin, type)
            || !ImageHeader.IsSafeToDecode(cover.Span))
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(thumbnails);
        if (!thumbnails.Exists)
        {
            return false;
        }

        // The ID is already restricted to [A-Za-z0-9-]; GetFileName also guarantees no folder part.
        var target = Path.Combine(thumbnails.FullName, Path.GetFileName($"thumbnail_{asin}_{type}_portrait.jpg"));
        var temp = target + ".paperroute-tmp";
        try
        {
            File.WriteAllBytes(temp, Scale(cover.ToArray()));
            File.Move(temp, target, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or ExternalException or OutOfMemoryException or InvalidOperationException)
        {
            // GDI+ reports unreadable images as ArgumentException/OutOfMemoryException.
            return false;
        }
        finally
        {
            DeleteIfLeft(temp);
        }
    }

    private static void DeleteIfLeft(string temp)
    {
        try
        {
            File.Delete(temp);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort.
        }
    }

    private static bool IsSafe(string asin, string type)
    {
        try
        {
            return SafeId().IsMatch(asin) && SafeType().IsMatch(type);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static byte[] Scale(byte[] cover)
    {
        using var input = new MemoryStream(cover, writable: false);
        using var original = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: true);
        var scale = Math.Min((double)MaxWidth / original.Width, (double)MaxHeight / original.Height);
        var width = Math.Max(1, (int)Math.Round(original.Width * scale));
        var height = Math.Max(1, (int)Math.Round(original.Height * scale));

        using var thumbnail = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(thumbnail))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(original, 0, 0, width, height);
        }

        var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, JpegQuality);
        using var output = new MemoryStream();
        thumbnail.Save(output, jpeg, parameters);
        return output.ToArray();
    }

    // The ID and type become part of a file name: allow only plain characters.
    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex SafeId();

    [GeneratedRegex("^[A-Z]{4}$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex SafeType();
}
