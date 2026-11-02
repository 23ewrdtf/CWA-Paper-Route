using PaperRoute.Logging;

namespace PaperRoute.Core.Tests;

public sealed class FileActivityLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "paperroute-log-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Writes_line_to_dated_file()
    {
        var log = new FileActivityLog(_dir, new FixedTime(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero)));

        log.Write("hello");
        log.Write("world");

        Assert.Equal(["hello", "world"], File.ReadAllLines(Path.Combine(_dir, "2026-11-01.log")));
    }

    [Fact]
    public void Keeps_only_newest_14_daily_files()
    {
        var clock = new FixedTime(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var log = new FileActivityLog(_dir, clock);

        for (var day = 0; day < 20; day++)
        {
            log.Write("line");
            clock.Advance(TimeSpan.FromDays(1));
        }

        var files = Directory.GetFiles(_dir).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(FileActivityLog.FilesKept, files.Count);
        Assert.Equal("2026-10-07.log", files[0]);
        Assert.Equal("2026-10-20.log", files[^1]);
    }

    [Fact]
    public void Does_not_delete_other_files_in_folder()
    {
        Directory.CreateDirectory(_dir);
        var other = Path.Combine(_dir, "notes.txt");
        File.WriteAllText(other, "keep me");
        var clock = new FixedTime(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var log = new FileActivityLog(_dir, clock);

        for (var day = 0; day < 20; day++)
        {
            log.Write("line");
            clock.Advance(TimeSpan.FromDays(1));
        }

        Assert.True(File.Exists(other));
    }

    [Fact]
    public void One_locked_old_log_does_not_stop_pruning_the_others()
    {
        Directory.CreateDirectory(_dir);
        for (var day = 1; day <= 20; day++)
        {
            File.WriteAllText(Path.Combine(_dir, $"2026-09-{day:00}.log"), "old");
        }

        // 09-07 is the first old file pruning deletes (newest-first after the 14 kept).
        using (new FileStream(Path.Combine(_dir, "2026-09-07.log"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            new FileActivityLog(_dir, new FixedTime(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero))).Write("new day");
        }

        // 21 files existed; 14 kept; the locked 09-07 stays, every other old one is gone.
        Assert.Equal(FileActivityLog.FilesKept + 1, Directory.GetFiles(_dir).Length);
    }

    [Fact]
    public void Unwritable_log_never_throws()
    {
        File.WriteAllText(_dir, "a file where the log folder should be");
        try
        {
            var log = new FileActivityLog(_dir);

            var error = Record.Exception(() => log.Write("line"));

            Assert.Null(error);
        }
        finally
        {
            File.Delete(_dir);
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
