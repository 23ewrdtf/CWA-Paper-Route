using System.Globalization;

using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace PaperRoute.Smtp;

/// <summary>Requires authentication, enforces the size limit and allows one recipient per message.</summary>
/// <remarks>
/// Any recipient address is allowed here because CWA sends its "Test e-mail" to the admin's own profile
/// address; <see cref="PaperRouteMessageStore"/> refuses books not addressed to the configured recipient.
/// Nothing is ever relayed: every message ends in Paper Route.
/// </remarks>
internal sealed class PaperRouteMailboxFilter(SessionGuard guard) : IMailboxFilter
{
    private const long BytesPerMegabyte = 1024 * 1024;

    public Task<bool> CanAcceptFromAsync(ISessionContext context, IMailbox from, int size, CancellationToken cancellationToken)
    {
        guard.EnsureClientAllowed(context);
        guard.EnsureAuthenticated(context);
        SessionGuard.ResetRecipients(context);
        if (size > guard.Config.MaxMessageMB * BytesPerMegabyte)
        {
            guard.Note($"REFUSED {SessionGuard.RemoteAddress(context)} declared {size} bytes - larger than the {guard.Config.MaxMessageMB} MB limit");
            throw SizeLimit(guard.Config.MaxMessageMB);
        }

        return Task.FromResult(true);
    }

    public Task<bool> CanDeliverToAsync(ISessionContext context, IMailbox to, IMailbox from, CancellationToken cancellationToken)
    {
        guard.EnsureAuthenticated(context);
        if (SessionGuard.AddRecipient(context) > 1)
        {
            guard.Note($"REFUSED {SessionGuard.RemoteAddress(context)} second recipient {to.User}@{to.Host}");
            throw SessionGuard.Refuse(SmtpReplyCode.MailboxUnavailable, "Only one recipient per message");
        }

        return Task.FromResult(true);
    }

    internal static SmtpResponseException SizeLimit(int maxMegabytes) =>
        SessionGuard.Refuse(
            SmtpReplyCode.SizeLimitExceeded,
            string.Create(CultureInfo.InvariantCulture, $"Book is larger than the {maxMegabytes} MB limit set in Paper Route"));
}
