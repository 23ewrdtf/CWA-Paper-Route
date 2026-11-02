using System.Buffers.Binary;

namespace PaperRoute.Devices.MassStorage;

/// <summary>Reads image dimensions from the file header, so oversized images are refused before decoding.</summary>
/// <remarks>Protects against "decompression bombs": tiny files that declare huge pixel sizes.</remarks>
internal static class ImageHeader
{
    /// <summary>Largest accepted width or height in pixels.</summary>
    public const int MaxDimension = 4096;

    /// <summary>Largest accepted cover file in bytes.</summary>
    public const int MaxBytes = 10 * 1024 * 1024;

    private static ReadOnlySpan<byte> PngMagic => [0x89, (byte)'P', (byte)'N', (byte)'G'];

    /// <summary>Returns whether the image may be handed to the decoder: known format, readable size, within limits.</summary>
    /// <param name="image">Image file bytes.</param>
    /// <returns><see langword="false"/> for unknown formats, unreadable headers and oversized images.</returns>
    public static bool IsSafeToDecode(ReadOnlySpan<byte> image) =>
        image.Length <= MaxBytes
        && TryReadSize(image) is var (width, height)
        && width is > 0 and <= MaxDimension
        && height is > 0 and <= MaxDimension;

    /// <summary>Reads the declared width and height of a JPEG, PNG or GIF.</summary>
    /// <param name="image">Image file bytes.</param>
    /// <returns>The size, or <see langword="null"/> when it cannot be read.</returns>
    public static (int Width, int Height)? TryReadSize(ReadOnlySpan<byte> image)
    {
        if (image.Length >= 24 && image.StartsWith(PngMagic) && image.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return (BinaryPrimitives.ReadInt32BigEndian(image[16..]), BinaryPrimitives.ReadInt32BigEndian(image[20..]));
        }

        if (image.Length >= 10 && image.StartsWith("GIF8"u8))
        {
            return (BinaryPrimitives.ReadUInt16LittleEndian(image[6..]), BinaryPrimitives.ReadUInt16LittleEndian(image[8..]));
        }

        return image.Length >= 4 && image[0] == 0xFF && image[1] == 0xD8 ? JpegSize(image) : null;
    }

    private static (int Width, int Height)? JpegSize(ReadOnlySpan<byte> image)
    {
        var position = 2;
        while (position + 4 <= image.Length)
        {
            if (image[position] != 0xFF)
            {
                return null;
            }

            var marker = image[position + 1];
            if (marker == 0xFF)
            {
                position++; // fill byte
                continue;
            }

            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7))
            {
                position += 2; // markers without a length
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(image[(position + 2)..]);
            if (length < 2)
            {
                return null;
            }

            // Start-of-frame markers carry the size: C0-CF except C4 (DHT), C8 (JPG) and CC (DAC).
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                return position + 9 <= image.Length
                    ? (BinaryPrimitives.ReadUInt16BigEndian(image[(position + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(image[(position + 5)..]))
                    : null;
            }

            position += 2 + length;
        }

        return null;
    }
}
