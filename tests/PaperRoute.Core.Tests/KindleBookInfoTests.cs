using FsCheck;
using FsCheck.Xunit;

using PaperRoute.Delivery;
using PaperRoute.Tests.Shared;

namespace PaperRoute.Core.Tests;

public sealed class KindleBookInfoTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 0xFF, 0xD9];

    [Fact]
    public void Reads_asin_type_and_cover()
    {
        var info = KindleBookInfo.TryRead(MobiBuilder.Build("e83f56a9-479b-45a9-8483-219008b1b053", "EBOK", Jpeg));

        Assert.NotNull(info);
        Assert.Equal("e83f56a9-479b-45a9-8483-219008b1b053", info.Asin);
        Assert.Equal("EBOK", info.CdeType);
        Assert.Equal(Jpeg, info.Cover?.ToArray());
    }

    [Fact]
    public void Asin_can_come_from_record_504() =>
        Assert.Equal("B000TEST01", KindleBookInfo.TryRead(MobiBuilder.Build(asinRecordType: 504))?.Asin);

    [Fact]
    public void Book_without_cover_has_no_cover() =>
        Assert.Null(KindleBookInfo.TryRead(MobiBuilder.Build(cover: null))?.Cover);

    [Fact]
    public void Cover_offset_past_last_record_gives_no_cover() =>
        Assert.Null(KindleBookInfo.TryRead(MobiBuilder.Build(cover: Jpeg, coverOffset: 50))?.Cover);

    [Fact]
    public void Cover_that_is_not_an_image_is_ignored() =>
        Assert.Null(KindleBookInfo.TryRead(MobiBuilder.Build(cover: [1, 2, 3, 4, 5, 6]))?.Cover);

    [Fact]
    public void No_exth_gives_no_details()
    {
        var info = KindleBookInfo.TryRead(MobiBuilder.Build(asin: null, cdeType: null, cover: null));

        Assert.NotNull(info);
        Assert.Null(info.Asin);
        Assert.Null(info.CdeType);
    }

    [Fact]
    public void Non_mobi_file_is_null() =>
        Assert.Null(KindleBookInfo.TryRead("%PDF-1.7 not a kindle book at all, padded to be long enough for a header"u8));

    [Property]
    public void Random_bytes_never_throw(byte[] junk) => _ = KindleBookInfo.TryRead(junk);

    [Property]
    public void Corrupted_real_header_never_throws(NonNegativeInt position, byte value)
    {
        ArgumentNullException.ThrowIfNull(position);
        var book = MobiBuilder.Build("B000TEST01", "EBOK", Jpeg);
        book[position.Get % book.Length] = value;

        _ = KindleBookInfo.TryRead(book);
    }
}
