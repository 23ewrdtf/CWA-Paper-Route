using System.Text;

// Test double for Calibre's "ebook-convert <input> <output>". The input's text picks the behaviour:
// "MODE:fail" exits 3, "MODE:hang" never finishes, "MODE:noout" exits 0 without output,
// "MODE:env" requires CALIBRE_TEMP_DIR and leaves a scratch file there,
// anything else writes a minimal AZW3 (BOOKMOBI at byte 60).
if (args is ["sleep", _])
{
    await Task.Delay(TimeSpan.FromSeconds(60));
    return 0;
}

if (args.Length != 2)
{
    await Console.Error.WriteLineAsync("usage: ebook-convert input output");
    return 2;
}

var mode = Encoding.ASCII.GetString(await File.ReadAllBytesAsync(args[0]));
if (mode.Contains("MODE:orphan", StringComparison.Ordinal))
{
    // Like a helper process left behind: a child that inherits our stdout/stderr and keeps them open.
    var self = Environment.ProcessPath!;
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(self, ["sleep", "x"]) { UseShellExecute = false });
}

if (mode.Contains("MODE:fail", StringComparison.Ordinal))
{
    await Console.Error.WriteLineAsync("Conversion error: this book is broken");
    return 3;
}

if (mode.StartsWith("MODE:hang", StringComparison.Ordinal))
{
    await Task.Delay(Timeout.Infinite);
}

if (mode.StartsWith("MODE:env", StringComparison.Ordinal))
{
    // Like Calibre: scratch files go to CALIBRE_TEMP_DIR.
    var scratch = Environment.GetEnvironmentVariable("CALIBRE_TEMP_DIR");
    if (string.IsNullOrEmpty(scratch))
    {
        await Console.Error.WriteLineAsync("CALIBRE_TEMP_DIR is not set");
        return 4;
    }

    await File.WriteAllTextAsync(Path.Join(scratch, "calibre-scratch.txt"), "book data");
}

if (mode.StartsWith("MODE:noout", StringComparison.Ordinal))
{
    return 0;
}

var book = new byte[2048];
Encoding.ASCII.GetBytes("BOOKMOBI").CopyTo(book, 60);
await File.WriteAllBytesAsync(args[1], book);
Console.WriteLine("Output saved to " + args[1]);
return 0;
