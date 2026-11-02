using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;

using PaperRoute.Devices.MassStorage;

namespace PaperRoute.Devices.Tests;

[Collection(GdiSerial.Name)]
public sealed class ImageHeaderTests
{
    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("gif")]
    public void Reads_real_image_sizes(string format)
    {
        var size = ImageHeader.TryReadSize(Encode(315, 500, format));

        Assert.Equal((315, 500), size);
    }

    [Fact]
    public void Huge_declared_png_is_refused() =>
        Assert.False(ImageHeader.IsSafeToDecode(Png(60_000, 60_000)));

    [Fact]
    public void Huge_declared_jpeg_is_refused() =>
        Assert.False(ImageHeader.IsSafeToDecode(Jpeg(50_000, 50_000)));

    [Fact]
    public void Huge_declared_gif_is_refused() =>
        Assert.False(ImageHeader.IsSafeToDecode([.. "GIF89a"u8, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0]));

    [Fact]
    public void Normal_cover_is_allowed() =>
        Assert.True(ImageHeader.IsSafeToDecode(Encode(315, 500, "jpeg")));

    [Fact]
    public void Unknown_or_truncated_header_is_refused() =>
        Assert.False(ImageHeader.IsSafeToDecode([0xFF, 0xD8, 0xFF, 0xE0, 0, 4, 1, 2]));

    [Fact]
    public void Too_many_bytes_is_refused() =>
        Assert.False(ImageHeader.IsSafeToDecode([.. Encode(100, 100, "jpeg"), .. new byte[ImageHeader.MaxBytes]]));

    [Fact]
    public void Bomb_cover_never_reaches_the_decoder_and_book_is_delivered()
    {
        var root = Path.Combine(Path.GetTempPath(), "paperroute-bomb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "system", "thumbnails"));
        try
        {
            new MassStorageReader(root, long.MaxValue).Write("a.azw3", global::PaperRoute.Tests.Shared.MobiBuilder.Build("B000BOMB01", "EBOK", Png(60_000, 60_000)));

            Assert.True(File.Exists(Path.Combine(root, "documents", "a.azw3")));
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "system", "thumbnails")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }

    private static byte[] Jpeg(int width, int height)
    {
        // SOI, then a baseline SOF0 segment declaring the size.
        byte[] bytes = [0xFF, 0xD8, 0xFF, 0xC0, 0, 17, 8, 0, 0, 0, 0, 3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1];
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(7), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(9), (ushort)width);
        return bytes;
    }

    private static byte[] Encode(int width, int height, string format)
    {
        using var bitmap = new Bitmap(width, height);
        using var stream = new MemoryStream();
        bitmap.Save(stream, format switch { "png" => ImageFormat.Png, "gif" => ImageFormat.Gif, _ => ImageFormat.Jpeg });
        return stream.ToArray();
    }
}
