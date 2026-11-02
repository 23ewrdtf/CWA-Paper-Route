namespace PaperRoute.Smtp;

/// <summary>SMTP time limits.</summary>
/// <param name="Command">Longest wait for the client's next command.</param>
/// <param name="Session">Longest whole session, including converting and copying the book.</param>
public sealed record SmtpTimeouts(TimeSpan Command, TimeSpan Session)
{
    /// <summary>Gets the defaults: 60 s per command; 15 min per session (CWA itself gives up after 10 min).</summary>
    public static SmtpTimeouts Default { get; } = new(TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(15));
}
