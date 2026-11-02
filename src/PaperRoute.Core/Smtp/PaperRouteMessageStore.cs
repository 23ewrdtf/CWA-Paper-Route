using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using MimeKit;

using PaperRoute.Delivery;

using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace PaperRoute.Smtp;

/// <summary>Hands each received message to <see cref="DeliveryService"/> and replies with its outcome.</summary>
internal sealed class PaperRouteMessageStore(SessionGuard guard, DeliveryService delivery, Action<DeliveryOutcome> delivered) : IMessageStore
{
    // Avoids copying the message when SmtpServer hands it over in one segment.
    private static MemoryStream AsStream(ReadOnlySequence<byte> buffer) =>
        buffer.IsSingleSegment && MemoryMarshal.TryGetArray(buffer.First, out var segment) && segment.Array is not null
            ? new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(buffer.ToArray(), writable: false);

    public async Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
    {
        guard.EnsureAuthenticated(context);
        if (transaction.To.Count != 1)
        {
            return new SmtpResponse(SmtpReplyCode.MailboxUnavailable, "Only one recipient per message");
        }

        if (buffer.Length > guard.Config.MaxMessageMB * 1024L * 1024L)
        {
            guard.Note($"REFUSED {SessionGuard.RemoteAddress(context)} {buffer.Length} bytes - larger than the {guard.Config.MaxMessageMB} MB limit");
            return PaperRouteMailboxFilter.SizeLimit(guard.Config.MaxMessageMB).Response;
        }

        MimeMessage message;
        try
        {
            using var stream = AsStream(buffer);
            message = await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (FormatException)
        {
            guard.Note($"REFUSED {SessionGuard.RemoteAddress(context)} message could not be read");
            return new SmtpResponse(SmtpReplyCode.TransactionFailed, "The message could not be read");
        }

        using (message)
        {
            return Handle(context, transaction, message);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "SMTP boundary: any unexpected failure must become a readable reply for CWA and a log line, not a dropped connection.")]
    private SmtpResponse Handle(ISessionContext context, IMessageTransaction transaction, MimeMessage message)
    {
        try
        {
            var recipient = $"{transaction.To[0].User}@{transaction.To[0].Host}";
            var hasAttachment = message.BodyParts.OfType<MimePart>().Any(part => !string.IsNullOrEmpty(part.FileName));
            if (hasAttachment && !string.Equals(recipient, guard.Config.Recipient, StringComparison.OrdinalIgnoreCase))
            {
                guard.Warn($"Refused a book addressed to {recipient} from {SessionGuard.RemoteAddress(context)}");
                return new SmtpResponse(SmtpReplyCode.MailboxUnavailable, $"Unknown recipient - Paper Route only accepts mail for {guard.Config.Recipient}");
            }

            var outcome = delivery.Deliver(message, SessionGuard.RemoteAddress(context));
            delivered(outcome);
            return new SmtpResponse((SmtpReplyCode)outcome.SmtpCode, outcome.Text);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            guard.Note($"ERROR {SessionGuard.RemoteAddress(context)} unexpected {e.GetType().Name}: {e.Message}");
            var outcome = new DeliveryOutcome(451, $"Paper Route hit an internal error ({e.GetType().Name}) - nothing was copied, send again", OutcomeKind.Failed);
            delivered(outcome);
            return new SmtpResponse((SmtpReplyCode)outcome.SmtpCode, outcome.Text);
        }
    }
}
