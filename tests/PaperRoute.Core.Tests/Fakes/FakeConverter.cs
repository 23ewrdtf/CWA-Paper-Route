using System.Text;

using PaperRoute.Delivery;

namespace PaperRoute.Core.Tests.Fakes;

internal sealed class FakeConverter : IBookConverter
{
    public bool IsAvailable => true;

    public string? Failure { get; init; }

    public byte[]? Output { get; init; }

    public int Calls { get; private set; }

    public TimeSpan Delay { get; init; }

    public BookAttachment ToAzw3(BookAttachment epub)
    {
        Calls++;
        if (Delay > TimeSpan.Zero)
        {
            Thread.Sleep(Delay);
        }

        if (Failure is not null)
        {
            throw new ConversionException(Failure);
        }

        var book = Output ?? new byte[2048];
        if (Output is null)
        {
            Encoding.ASCII.GetBytes("BOOKMOBI").CopyTo(book, 60);
        }

        return new BookAttachment(Path.ChangeExtension(epub.FileName, ".azw3"), book);
    }
}
