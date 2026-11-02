using System.Buffers.Binary;
using System.Text;

namespace PaperRoute.Tests.Shared;

/// <summary>Builds minimal MOBI/AZW3 files (PDB + MOBI header + EXTH + image records) for tests.</summary>
internal static class MobiBuilder
{
    private const int MobiHeaderLength = 232;

    public static byte[] Build(string? asin = "B000TEST01", string? cdeType = "EBOK", byte[]? cover = null, int? asinRecordType = 113, uint? coverOffset = 0)
    {
        var exth = new List<(int Type, byte[] Data)>();
        if (asin is not null && asinRecordType is not null)
        {
            exth.Add((asinRecordType.Value, Encoding.UTF8.GetBytes(asin)));
        }

        if (cdeType is not null)
        {
            exth.Add((501, Encoding.ASCII.GetBytes(cdeType)));
        }

        if (cover is not null && coverOffset is not null)
        {
            var offset = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(offset, coverOffset.Value);
            exth.Add((201, offset));
        }

        var record0 = BuildRecord0(exth, firstImageIndex: 1);
        var records = cover is null ? new[] { record0 } : new[] { record0, cover };
        return Assemble(records);
    }

    private static byte[] BuildRecord0(List<(int Type, byte[] Data)> exth, uint firstImageIndex)
    {
        using var stream = new MemoryStream();
        stream.Write(new byte[16]); // PalmDOC header
        var mobi = new byte[MobiHeaderLength];
        Encoding.ASCII.GetBytes("MOBI").CopyTo(mobi, 0);
        BinaryPrimitives.WriteUInt32BigEndian(mobi.AsSpan(4), MobiHeaderLength);
        BinaryPrimitives.WriteUInt32BigEndian(mobi.AsSpan(0x5C), firstImageIndex);
        BinaryPrimitives.WriteUInt32BigEndian(mobi.AsSpan(0x70), exth.Count > 0 ? 0x40u : 0u);
        stream.Write(mobi);
        if (exth.Count > 0)
        {
            using var body = new MemoryStream();
            var head = new byte[8];
            foreach (var (type, data) in exth)
            {
                BinaryPrimitives.WriteUInt32BigEndian(head, (uint)type);
                BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(4), (uint)(data.Length + 8));
                body.Write(head);
                body.Write(data);
            }

            Span<byte> exthHead = stackalloc byte[12];
            Encoding.ASCII.GetBytes("EXTH").CopyTo(exthHead);
            BinaryPrimitives.WriteUInt32BigEndian(exthHead[4..], (uint)(body.Length + 12));
            BinaryPrimitives.WriteUInt32BigEndian(exthHead[8..], (uint)exth.Count);
            stream.Write(exthHead);
            stream.Write(body.ToArray());
        }

        return stream.ToArray();
    }

    private static byte[] Assemble(byte[][] records)
    {
        var headerLength = 78 + (8 * records.Length) + 2;
        var file = new byte[headerLength + records.Sum(r => r.Length)];
        Encoding.ASCII.GetBytes("Test book").CopyTo(file, 0);
        Encoding.ASCII.GetBytes("BOOKMOBI").CopyTo(file, 60);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(76), (ushort)records.Length);
        var offset = headerLength;
        for (var i = 0; i < records.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(78 + (8 * i)), (uint)offset);
            records[i].CopyTo(file, offset);
            offset += records[i].Length;
        }

        return file;
    }
}
