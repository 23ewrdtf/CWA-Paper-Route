using System.Globalization;

namespace PaperRoute.Logging;

/// <summary>Writes the activity log to one file per day and keeps the newest files only.</summary>
public sealed class FileActivityLog : IActivityLog
{
    /// <summary>Number of daily log files kept.</summary>
    public const int FilesKept = 14;

    private readonly string _directory;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="FileActivityLog"/> class.</summary>
    /// <param name="directory">Folder for the <c>yyyy-MM-dd.log</c> files; created when missing.</param>
    /// <param name="time">Clock; the system clock when omitted.</param>
    public FileActivityLog(string directory, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        _directory = directory;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public void Write(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var date = _time.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var path = Path.Combine(_directory, date + ".log");
        lock (_gate)
        {
            try
            {
                var isNewFile = !File.Exists(path);
                Directory.CreateDirectory(_directory);
                File.AppendAllText(path, line + Environment.NewLine);
                if (isNewFile)
                {
                    Prune();
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Logging must never break a delivery (log file locked by an editor, disk full, ...).
            }
        }
    }

    private void Prune()
    {
        var old = Directory.GetFiles(_directory, "????-??-??.log")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Skip(FilesKept);
        foreach (var file in old)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A file open in an editor is skipped today and removed on a later day.
            }
        }
    }
}
