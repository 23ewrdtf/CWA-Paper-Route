namespace PaperRoute.Smtp;

/// <summary>Data for <see cref="SmtpHost.SecurityWarning"/>.</summary>
/// <param name="message">Plain-language description, for example <c>Refused wrong login or password from 127.0.0.1</c>.</param>
public sealed class SecurityWarningEventArgs(string message) : EventArgs
{
    /// <summary>Gets the description of what was refused.</summary>
    public string Message { get; } = message;
}
