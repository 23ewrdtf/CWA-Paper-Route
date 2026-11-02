using PaperRoute.Delivery;

namespace PaperRoute.Smtp;

/// <summary>Data for <see cref="SmtpHost.Delivered"/>.</summary>
/// <param name="outcome">What happened to the message.</param>
public sealed class DeliveredEventArgs(DeliveryOutcome outcome) : EventArgs
{
    /// <summary>Gets what happened to the message.</summary>
    public DeliveryOutcome Outcome { get; } = outcome;
}
