using System.Globalization;
using System.Net;

using MimeKit;

using PaperRoute.Config;
using PaperRoute.Logging;

namespace PaperRoute.Delivery;

/// <summary>Decides what to do with a received message and copies the book to the reader.</summary>
public sealed class DeliveryService
{
    private const string EpubExtension = "epub";
    private const long BytesPerMegabyte = 1024 * 1024;

    private readonly IDeviceLocator _locator;
    private readonly AppConfig _config;
    private readonly IActivityLog _log;
    private readonly IBookConverter? _converter;
    private readonly Lock _gate;

    /// <summary>Initializes a new instance of the <see cref="DeliveryService"/> class.</summary>
    /// <param name="locator">Finds connected readers.</param>
    /// <param name="config">Current settings.</param>
    /// <param name="log">Activity log; receives one line per message.</param>
    /// <param name="converter">Converts EPUB to AZW3; <see langword="null"/> when none is available.</param>
    /// <param name="gate">Lock shared by every service that may copy to the same reader; a private one when <see langword="null"/>.</param>
    public DeliveryService(IDeviceLocator locator, AppConfig config, IActivityLog log, IBookConverter? converter = null, Lock? gate = null)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(log);
        _locator = locator;
        _config = config;
        _log = log;
        _converter = converter;
        _gate = gate ?? new Lock();
    }

    /// <summary>Handles one received message. Deliveries run one at a time.</summary>
    /// <param name="message">The parsed message.</param>
    /// <param name="client">Address of the SMTP client, for the log.</param>
    /// <returns>The reply to send to the client.</returns>
    public DeliveryOutcome Deliver(MimeMessage message, IPAddress client)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(client);
        var attachments = AttachmentExtractor.Extract(message);
        DeliveryOutcome outcome;
        lock (_gate)
        {
            outcome = Decide(attachments);
        }

        var names = attachments.Count == 0 ? "-" : string.Join(",", attachments.Select(a => a.FileName));
        var size = attachments.Sum(a => (long)a.Content.Length);
        _log.Write(string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {client} {names} {size} {outcome.SmtpCode} {outcome.Text}"));
        return outcome;
    }

    private static string Megabytes(long bytes) =>
        ((bytes + BytesPerMegabyte - 1) / BytesPerMegabyte).ToString(CultureInfo.InvariantCulture);

    private static DeliveryOutcome NotARealFile(BookAttachment book) =>
        new(550, $"This doesn't look like a real {book.Extension.ToUpperInvariant()} file - nothing was copied", OutcomeKind.Rejected);

    private DeliveryOutcome Decide(IReadOnlyList<BookAttachment> attachments)
    {
        if (attachments.Count == 0)
        {
            return new(250, "Test e-mail received - CWA is connected", OutcomeKind.TestMessage);
        }

        var (selected, refusal) = SelectBook(attachments);
        if (selected is null)
        {
            return refusal!;
        }

        var book = selected;

        // Check the Kindle before a slow conversion.
        var readers = _locator.FindReaders();
        if (readers.Count == 0)
        {
            return new(550, "Kindle not connected - plug it in and send again", OutcomeKind.Rejected);
        }

        if (readers.Count > 1)
        {
            return new(550, "More than one Kindle connected - unplug all but one", OutcomeKind.Rejected);
        }

        if (string.Equals(book.Extension, EpubExtension, StringComparison.Ordinal))
        {
            var (converted, failure) = Convert(book);
            if (converted is null)
            {
                return failure!;
            }

            book = converted;
        }

        return Copy(book, readers[0]);
    }

    /// <summary>Picks the one book in the message and checks it, without touching any device.</summary>
    private (BookAttachment? Book, DeliveryOutcome? Refusal) SelectBook(IReadOnlyList<BookAttachment> attachments)
    {
        var books = attachments
            .Where(a => _config.BookExtensionsOrDefault.Contains(a.Extension, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (books.Count == 0)
        {
            return (null, new(550, "No e-book attachment found", OutcomeKind.Rejected));
        }

        if (books.Count > 1)
        {
            return (null, new(550, "Only one book per message", OutcomeKind.Rejected));
        }

        var book = books[0];
        if (!BookSignature.Matches(book.Extension, book.Content.Span))
        {
            return (null, NotARealFile(book));
        }

        if (string.Equals(book.Extension, EpubExtension, StringComparison.Ordinal) && _converter is not { IsAvailable: true })
        {
            return (null, new(550, "Install Calibre (calibre-ebook.com) so Paper Route can convert EPUB books", OutcomeKind.Rejected));
        }

        return (book, null);
    }

    private (BookAttachment? Book, DeliveryOutcome? Refusal) Convert(BookAttachment epub)
    {
        try
        {
            var converted = _converter!.ToAzw3(epub);
            return BookSignature.Matches(converted.Extension, converted.Content.Span)
                ? (converted, null)
                : (null, NotARealFile(converted));
        }
        catch (ConversionException e)
        {
            return (null, new(451, $"Converting {epub.FileName} to AZW3 failed: {e.Message} - nothing was copied", OutcomeKind.Failed));
        }
    }

    private static DeliveryOutcome Copy(BookAttachment book, IReaderDevice reader)
    {
        try
        {
            reader.Write(book.FileName, book.Content);
        }
        catch (InsufficientSpaceException e)
        {
            return new(
                550,
                $"Not enough space on {reader.DisplayName}: {Megabytes(e.NeededBytes)} MB needed, {Megabytes(e.FreeBytes)} MB free",
                OutcomeKind.Rejected);
        }
        catch (DeviceWriteException e)
        {
            return new(451, $"Copy to {reader.DisplayName} failed: {e.Message} - nothing was saved, send again", OutcomeKind.Failed);
        }

        return new(250, $"Delivered {book.FileName} to {reader.DisplayName}", OutcomeKind.Delivered);
    }
}
