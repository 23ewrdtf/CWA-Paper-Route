namespace PaperRoute.Delivery;

/// <summary>What happened to a received message.</summary>
public enum OutcomeKind
{
    /// <summary>A message without attachments, such as CWA's "Test e-mail".</summary>
    TestMessage,

    /// <summary>The book is on the reader.</summary>
    Delivered,

    /// <summary>The message was refused; retrying unchanged will not help.</summary>
    Rejected,

    /// <summary>Copying failed; retrying may help.</summary>
    Failed,
}
