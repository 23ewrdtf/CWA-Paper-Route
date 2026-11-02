namespace PaperRoute.Smtp;

/// <summary>Result of starting or restarting the SMTP listener.</summary>
/// <param name="Ok">Whether the listener is running with the requested settings.</param>
/// <param name="Error">Plain-language reason when <paramref name="Ok"/> is <see langword="false"/>.</param>
public sealed record StartResult(bool Ok, string? Error);
