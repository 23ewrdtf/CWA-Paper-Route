namespace PaperRoute.Delivery;

/// <summary>The SMTP reply for a message and how to report it.</summary>
/// <param name="SmtpCode">SMTP reply code.</param>
/// <param name="Text">Reply text; CWA shows it to the user verbatim.</param>
/// <param name="Kind">Category of the outcome.</param>
public sealed record DeliveryOutcome(int SmtpCode, string Text, OutcomeKind Kind);
