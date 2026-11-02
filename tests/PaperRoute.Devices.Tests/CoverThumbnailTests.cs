using System.Drawing;
using System.Drawing.Imaging;

using PaperRoute.Devices.MassStorage;
using PaperRoute.Tests.Shared;

namespace PaperRoute.Devices.Tests;

[Collection(GdiSerial.Name)]
public sealed class CoverThumbnailTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "paperroute-thumb-" + Guid.NewGuid().ToString("N"));

    public CoverThumbnailTests() => Directory.CreateDirectory(Path.Combine(_root, "system", "thumbnails"));

    private string Thumbnails => Path.Combine(_root, "system", "thumbnails");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Delivery_writes_thumbnail_named_like_the_kindle_expects()
    {
        Reader().Write("Ubik.azw3", MobiBuilder.Build("e83f56a9-479b-45a9-8483-219008b1b053", "EBOK", Jpeg(315, 500)));

        var thumbnail = Path.Combine(Thumbnails, "thumbnail_e83f56a9-479b-45a9-8483-219008b1b053_EBOK_portrait.jpg");
        Assert.True(File.Exists(thumbnail));
        using var image = Image.FromFile(thumbnail);
        Assert.Equal(ImageFormat.Jpeg.Guid, image.RawFormat.Guid);
        Assert.True(image.Width <= CoverThumbnail.MaxWidth && image.Height <= CoverThumbnail.MaxHeight);
        Assert.Equal(CoverThumbnail.MaxHeight, image.Height); // tall cover: height is the limit
        Assert.InRange(image.Width, 206, 210); // 315x500 scaled to height 330 keeps the shape
    }

    [Fact]
    public void Wide_cover_is_limited_by_width()
    {
        Reader().Write("a.azw3", MobiBuilder.Build("B000WIDE01", "EBOK", Jpeg(800, 400)));

        using var image = Image.FromFile(Path.Combine(Thumbnails, "thumbnail_B000WIDE01_EBOK_portrait.jpg"));
        Assert.Equal(CoverThumbnail.MaxWidth, image.Width);
        Assert.InRange(image.Height, 105, 109);
    }

    [Fact]
    public void No_thumbnails_folder_means_no_thumbnail_but_book_is_delivered()
    {
        Directory.Delete(Path.Combine(_root, "system"), recursive: true);

        Reader().Write("a.azw3", MobiBuilder.Build("B000TEST01", "EBOK", Jpeg(100, 150)));

        Assert.True(File.Exists(Path.Combine(_root, "documents", "a.azw3")));
        Assert.False(Directory.Exists(Path.Combine(_root, "system")));
    }

    [Theory]
    [InlineData("../../evil")]
    [InlineData("..\\evil")]
    [InlineData("a:b")]
    [InlineData("")]
    public void Unsafe_ids_are_ignored(string asin)
    {
        Reader().Write("a.azw3", MobiBuilder.Build(asin, "EBOK", Jpeg(100, 150)));

        Assert.Empty(Directory.GetFiles(Thumbnails));
        Assert.Empty(Directory.GetFiles(_root, "*.jpg", SearchOption.AllDirectories));
    }

    [Fact]
    public void Unsafe_type_is_ignored()
    {
        Reader().Write("a.azw3", MobiBuilder.Build("B000TEST01", "EB/K", Jpeg(100, 150)));

        Assert.Empty(Directory.GetFiles(Thumbnails));
    }

    [Fact]
    public void Broken_cover_image_still_delivers_the_book()
    {
        byte[] notReallyJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

        Reader().Write("a.azw3", MobiBuilder.Build("B000TEST01", "EBOK", notReallyJpeg));

        Assert.True(File.Exists(Path.Combine(_root, "documents", "a.azw3")));
        Assert.Empty(Directory.GetFiles(Thumbnails));
    }

    [Fact]
    public void Book_without_cover_writes_no_thumbnail()
    {
        Reader().Write("a.azw3", MobiBuilder.Build("B000TEST01", "EBOK", cover: null));

        Assert.Empty(Directory.GetFiles(Thumbnails));
    }

    [Fact]
    public void Kindle_partial_file_is_left_alone_and_real_thumbnail_added()
    {
        var partial = Path.Combine(Thumbnails, "thumbnail_B000TEST01_EBOK_portrait.jpg.partial");
        File.WriteAllBytes(partial, []);

        Reader().Write("a.azw3", MobiBuilder.Build("B000TEST01", "EBOK", Jpeg(100, 150)));

        Assert.True(File.Exists(partial));
        Assert.True(File.Exists(Path.Combine(Thumbnails, "thumbnail_B000TEST01_EBOK_portrait.jpg")));
        Assert.Empty(Directory.GetFiles(Thumbnails, "*.paperroute-tmp"));
    }

    [Fact]
    public void Read_only_thumbnail_folder_still_delivers_the_book()
    {
        var target = Path.Combine(Thumbnails, "thumbnail_B000TEST01_EBOK_portrait.jpg");
        File.WriteAllBytes(target, [1]);
        File.SetAttributes(target, FileAttributes.ReadOnly);
        try
        {
            var error = Record.Exception(() => Reader().Write("a.azw3", MobiBuilder.Build("B000TEST01", "EBOK", Jpeg(100, 150))));

            Assert.Null(error);
            Assert.True(File.Exists(Path.Combine(_root, "documents", "a.azw3")));
            Assert.Empty(Directory.GetFiles(Thumbnails, "*.paperroute-tmp"));
        }
        finally
        {
            File.SetAttributes(target, FileAttributes.Normal);
        }
    }

    private static byte[] Jpeg(int width, int height)
    {
        using var bitmap = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.SteelBlue);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Jpeg);
        return stream.ToArray();
    }

    private MassStorageReader Reader() => new(_root, long.MaxValue);
}
