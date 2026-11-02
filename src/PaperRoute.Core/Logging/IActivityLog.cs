namespace PaperRoute.Logging;

/// <summary>Receives one line per handled message or security event.</summary>
public interface IActivityLog
{
    /// <summary>Appends a line to the log.</summary>
    /// <param name="line">Text without a trailing newline.</param>
    void Write(string line);
}
