using System.Net;

using PaperRoute.Config;

using SmtpServer;
using SmtpServer.Net;
using SmtpServer.Protocol;

namespace PaperRoute.Smtp;

/// <summary>Per-listener security checks shared by the authenticator, mailbox filter and message store.</summary>
internal sealed class SessionGuard(AppConfig config, ClientFilter clients, Action<string> warn, Action<string> note)
{
    internal const string OverLimitKey = "PaperRoute:OverLimit";
    private const string FailedLoginsKey = "PaperRoute:FailedLogins";
    private const int MaxFailedLogins = 3;
    private const string RecipientsKey = "PaperRoute:Recipients";

    public AppConfig Config { get; } = config;

    public void Warn(string message) => warn(message);

    /// <summary>Writes a line to the activity log only (no notification).</summary>
    public void Note(string message) => note(message);

    public static IPAddress RemoteAddress(ISessionContext context) =>
        context.Properties.TryGetValue(EndpointListener.RemoteEndPointKey, out var value) && value is IPEndPoint endpoint
            ? endpoint.Address
            : IPAddress.None;

    /// <summary>Refuses the session (and closes it) when the client is not allowed or too many sessions are open.</summary>
    public void EnsureClientAllowed(ISessionContext context)
    {
        var address = RemoteAddress(context);
        if (!clients.IsAllowed(address))
        {
            warn($"Refused connection from {address} (not an allowed client)");
            throw Refuse(SmtpReplyCode.TransactionFailed, "Connections are only accepted from this PC");
        }

        if (context.Properties.ContainsKey(OverLimitKey))
        {
            throw Refuse(SmtpReplyCode.ServiceUnavailable, "Too many connections - try again in a moment");
        }
    }

    public void EnsureAuthenticated(ISessionContext context)
    {
        if (!context.Authentication.IsAuthenticated)
        {
            warn($"Refused unauthenticated mail from {RemoteAddress(context)}");
            throw Refuse(SmtpReplyCode.AuthenticationRequired, "Authentication required - enter Paper Route's login and password in CWA's e-mail settings");
        }
    }

    /// <summary>Logs a failed login; the third one in a session closes the connection.</summary>
    /// <remarks>
    /// The first failures keep the connection open on purpose: Python's smtplib (used by CWA) tries the
    /// next AUTH mechanism after a failure, and a closed connection would replace our text with
    /// "Connection unexpectedly closed".
    /// </remarks>
    public SmtpResponseException FailedLogin(ISessionContext context)
    {
        var failures = (context.Properties.TryGetValue(FailedLoginsKey, out var value) ? (int)value : 0) + 1;
        context.Properties[FailedLoginsKey] = failures;
        warn($"Refused wrong login or password from {RemoteAddress(context)}");
        return Refuse(
            SmtpReplyCode.AuthenticationFailed,
            "Wrong login or password - copy them again from Paper Route's Settings window",
            quit: failures >= MaxFailedLogins);
    }

    /// <summary>Counts recipients in the current session; returns the new count.</summary>
    public static int AddRecipient(ISessionContext context)
    {
        var count = (context.Properties.TryGetValue(RecipientsKey, out var value) ? (int)value : 0) + 1;
        context.Properties[RecipientsKey] = count;
        return count;
    }

    public static void ResetRecipients(ISessionContext context) => context.Properties.Remove(RecipientsKey);

    /// <summary>Builds a refusal that, by default, also closes the connection.</summary>
    /// <remarks>
    /// Closing is deliberate: CWA sends one message per connection, so nothing is lost, a misbehaving
    /// client gets no second try, and SmtpServer only sends our text verbatim (without its
    /// ", N retry(ies) remaining." suffix) when the session ends.
    /// </remarks>
    public static SmtpResponseException Refuse(SmtpReplyCode code, string text, bool quit = true) => new(new SmtpResponse(code, text), quit);
}
