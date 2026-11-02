using System.Buffers.Binary;
using System.Text;

namespace PaperRoute.Delivery;

/// <summary>Details from a MOBI/AZW3 header that Kindles use to name cover thumbnails.</summary>
/// <param name="Asin">Book ID (EXTH 113, else 504); Calibre stores the book's UUID here.</param>
/// <param name="CdeType">Content type (EXTH 501), for example <c>EBOK</c> or <c>PDOC</c>.</param>
/// <param name="Cover">Cover image bytes (JPEG, PNG or GIF), when the book has one.</param>
public sealed record KindleBookInfo(string? Asin, string? CdeType, ReadOnlyMemory<byte>? Cover)
{
    private const int PdbHeaderLength = 78;
    private const int MobiTypeOffset = 60;
    private const int PalmDocHeaderLength = 16;
    private const int FirstImageIndexOffset = 0x6C;
    private const int ExthFlagsOffset = 0x80;
    private const uint HasExth = 0x40;
    private const uint NoImages = 0xFFFFFFFF;

    /// <summary>Reads the header of a MOBI/AZW3 book. Never throws on malformed input.</summary>
    /// <param name="book">The book file.</param>
    /// <returns>The details, or <see langword="null"/> when the file is not a MOBI-family book.</returns>
    public static KindleBookInfo? TryRead(ReadOnlySpan<byte> book)
    {
        if (book.Length < PdbHeaderLength
            || !(book.Slice(MobiTypeOffset, 8).SequenceEqual("BOOKMOBI"u8) || book.Slice(MobiTypeOffset, 8).SequenceEqual("TEXtREAd"u8)))
        {
            return null;
        }

        var records = BinaryPrimitives.ReadUInt16BigEndian(book[76..]);
        var record0 = Record(book, records, 0);
        if (record0.Length < PalmDocHeaderLength + 8 || !record0.Slice(PalmDocHeaderLength, 4).SequenceEqual("MOBI"u8))
        {
            return new KindleBookInfo(null, null, null);
        }

        string? asin113 = null, asin504 = null, cdeType = null;
        uint? coverOffset = null;
        foreach (var (type, data) in ExthRecords(record0))
        {
            switch (type)
            {
                case 113: asin113 = Text(data); break;
                case 504: asin504 = Text(data); break;
                case 501: cdeType = Text(data); break;
                case 201 when data.Length >= 4: coverOffset = BinaryPrimitives.ReadUInt32BigEndian(data); break;
            }
        }

        return new KindleBookInfo(asin113 ?? asin504, cdeType, ReadCover(book, records, record0, coverOffset));
    }

    private static ReadOnlyMemory<byte>? ReadCover(ReadOnlySpan<byte> book, int records, ReadOnlySpan<byte> record0, uint? coverOffset)
    {
        if (coverOffset is null || record0.Length < FirstImageIndexOffset + 4)
        {
            return null;
        }

        var firstImage = BinaryPrimitives.ReadUInt32BigEndian(record0[FirstImageIndexOffset..]);
        var index = (long)firstImage + coverOffset.Value;
        if (firstImage == NoImages || index >= records)
        {
            return null;
        }

        var image = Record(book, records, (int)index);
        return IsImage(image) ? (ReadOnlyMemory<byte>?)image.ToArray() : null;
    }

    private static List<(uint Type, byte[] Data)> ExthRecords(ReadOnlySpan<byte> record0)
    {
        var result = new List<(uint, byte[])>();
        if (record0.Length < ExthFlagsOffset + 4 || (BinaryPrimitives.ReadUInt32BigEndian(record0[ExthFlagsOffset..]) & HasExth) == 0)
        {
            return result;
        }

        var start = PalmDocHeaderLength + (long)BinaryPrimitives.ReadUInt32BigEndian(record0[(PalmDocHeaderLength + 4)..]);
        if (start + 12 > record0.Length || !record0.Slice((int)start, 4).SequenceEqual("EXTH"u8))
        {
            return result;
        }

        var count = BinaryPrimitives.ReadUInt32BigEndian(record0[((int)start + 8)..]);
        var position = start + 12;
        for (var i = 0u; i < count && position + 8 <= record0.Length; i++)
        {
            var type = BinaryPrimitives.ReadUInt32BigEndian(record0[(int)position..]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(record0[((int)position + 4)..]);
            if (length < 8 || position + length > record0.Length)
            {
                break;
            }

            result.Add((type, record0.Slice((int)position + 8, (int)length - 8).ToArray()));
            position += length;
        }

        return result;
    }

    private static ReadOnlySpan<byte> Record(ReadOnlySpan<byte> book, int records, int index)
    {
        var entry = PdbHeaderLength + (8L * index);
        if (index >= records || entry + 4 > book.Length)
        {
            return [];
        }

        var start = (long)BinaryPrimitives.ReadUInt32BigEndian(book[(int)entry..]);
        var nextEntry = entry + 8;
        long end = index + 1 < records && nextEntry + 4 <= book.Length
            ? BinaryPrimitives.ReadUInt32BigEndian(book[(int)nextEntry..])
            : book.Length;
        return start < end && end <= book.Length ? book[(int)start..(int)end] : [];
    }

    private static bool IsImage(ReadOnlySpan<byte> data) =>
        data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]) // JPEG
        || data.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G']) // PNG
        || data.StartsWith("GIF8"u8);

    private static string? Text(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data).Trim('\0', ' ');
        return text.Length == 0 ? null : text;
    }
}
