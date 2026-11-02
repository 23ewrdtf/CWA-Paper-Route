using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

using PaperRoute.Delivery;

namespace PaperRoute.Devices.Conversion;

/// <summary>Converts EPUB to AZW3 with Calibre's <c>ebook-convert.exe</c>.</summary>
/// <remarks>
/// Runs Calibre as a separate process without a shell, with fixed file names in a private temporary
/// folder that is always deleted afterwards, and stops it after <c>timeout</c>.
/// </remarks>
/// <param name="exePath">Full path of <c>ebook-convert.exe</c>.</param>
/// <param name="timeout">Longest a conversion may take.</param>
/// <param name="tempRoot">Folder for temporary files; the user's temp folder when <see langword="null"/>.</param>
public sealed class CalibreConverter(string exePath, TimeSpan timeout, string? tempRoot = null) : IBookConverter
{
    /// <inheritdoc/>
    public bool IsAvailable => File.Exists(exePath);

    /// <inheritdoc/>
    public BookAttachment ToAzw3(BookAttachment epub)
    {
        ArgumentNullException.ThrowIfNull(epub);
        var work = Path.Combine(tempRoot ?? Path.Combine(Path.GetTempPath(), "PaperRoute"), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var input = Path.Combine(work, "book.epub");
            var output = Path.Combine(work, "book.azw3");
            File.WriteAllBytes(input, epub.Content.ToArray());
            Run(input, output);
            if (!File.Exists(output))
            {
                throw new ConversionException("Calibre did not produce a book");
            }

            return new BookAttachment(Path.ChangeExtension(epub.FileName, ".azw3"), File.ReadAllBytes(output));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ConversionException(e.Message, e);
        }
        finally
        {
            TryDelete(work);
        }
    }

    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(10);

    private static string Completed(Task<string> output) => output.IsCompletedSuccessfully ? output.Result : string.Empty;

    private static void StopTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            _ = process.WaitForExit(OutputDrainTimeout);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or AggregateException)
        {
            // Already exited, or part of the tree could not be stopped; the job object stops the rest.
        }
    }

    private static string LastLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? string.Empty;

    private static void TryDelete(string folder)
    {
        // Stopped helper processes release their files a moment after the job closes.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(folder, recursive: true);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(200);
            }
        }
    }

    private void Run(string input, string output)
    {
        var start = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(input)!,
        };
        start.ArgumentList.Add(input);
        start.ArgumentList.Add(output);

        // Calibre's own scratch files (unpacked book) go into our private folder, which is always deleted.
        var scratch = Path.Combine(Path.GetDirectoryName(input)!, "calibre-temp");
        Directory.CreateDirectory(scratch);
        start.Environment["CALIBRE_TEMP_DIR"] = scratch;

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new ConversionException("Calibre could not be started");
        }
        catch (Win32Exception e)
        {
            throw new ConversionException($"Calibre could not be started: {e.Message}", e);
        }

        using (process)
        using (var job = KillOnCloseJob.TryCreate())
        {
            _ = job?.TryAdd(process);
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            var finished = process.WaitForExit(timeout);
            if (!finished)
            {
                StopTree(process);
            }

            // Closing the job stops helper processes Calibre may have left behind; they would otherwise keep
            // the output pipes and the temp folder open.
            job?.Dispose();
            if (!finished)
            {
                throw new ConversionException(string.Create(CultureInfo.InvariantCulture, $"Calibre took longer than {timeout.TotalMinutes:0.#} minutes"));
            }

            _ = Task.WaitAll([stdout, stderr], OutputDrainTimeout);
            if (process.ExitCode != 0)
            {
                var reason = LastLine(Completed(stderr));
                if (reason.Length == 0)
                {
                    reason = LastLine(Completed(stdout));
                }

                throw new ConversionException(string.Create(CultureInfo.InvariantCulture, $"Calibre exited with code {process.ExitCode}: {reason}"));
            }
        }
    }
}
