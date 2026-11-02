using System.Diagnostics.CodeAnalysis;

using PaperRoute.Delivery;

namespace PaperRoute.Devices.MassStorage;

/// <summary>A Kindle connected as a USB drive.</summary>
public sealed class MassStorageReader : IReaderDevice
{
    private const string TempSuffix = ".paperroute-tmp";

    private readonly string _root;
    private readonly Func<bool> _stillConnected;

    /// <summary>Initializes a new instance of the <see cref="MassStorageReader"/> class.</summary>
    /// <param name="root">Root folder of the drive, for example <c>K:\</c>.</param>
    /// <param name="freeBytes">Free space on the drive in bytes.</param>
    public MassStorageReader(string root, long freeBytes)
        : this(root, freeBytes, () => true)
    {
    }

    internal MassStorageReader(string root, long freeBytes, Func<bool> stillConnected)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
        _stillConnected = stillConnected;
        FreeBytes = freeBytes;
        var letter = Path.GetPathRoot(root)?.TrimEnd('\\', '/');
        DisplayName = letter is { Length: 2 } && letter[1] == ':' ? $"Kindle ({letter})" : "Kindle";
    }

    /// <inheritdoc/>
    public string DisplayName { get; }

    /// <inheritdoc/>
    public DeviceKind Kind => DeviceKind.MassStorage;

    /// <inheritdoc/>
    public long FreeBytes { get; }

    /// <inheritdoc/>
    public void Write(string fileName, ReadOnlyMemory<byte> content)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        if (fileName.Length == 0 || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("Expected a plain file name without folders", nameof(fileName));
        }

        if (content.Length > FreeBytes)
        {
            throw new InsufficientSpaceException(content.Length, FreeBytes);
        }

        if (!_stillConnected())
        {
            throw new DeviceWriteException("the Kindle is no longer connected (its drive letter now belongs to something else)");
        }

        var documents = Path.Combine(_root, "documents");
        var target = Path.Combine(documents, fileName);
        var temp = target + TempSuffix;
        try
        {
            Directory.CreateDirectory(documents);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, FileOptions.WriteThrough))
            {
                stream.Write(content.Span);
                stream.Flush(flushToDisk: true);
            }

            if (new FileInfo(temp).Length != content.Length)
            {
                throw new IOException("the copied file has the wrong size");
            }

            File.Move(temp, target, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            throw new DeviceWriteException(e.Message, e);
        }

        // The book is in place; the cover thumbnail is a best-effort extra.
        TryWriteThumbnail(content.Span);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The book is already delivered; nothing about the optional cover may turn that into a failure.")]
    private void TryWriteThumbnail(ReadOnlySpan<byte> book)
    {
        try
        {
            _ = CoverThumbnail.TryWrite(new DirectoryInfo(Path.Combine(_root, "system", "thumbnails")), book);
        }
        catch (Exception e)
        {
            // Best effort: note it for diagnostics only.
            System.Diagnostics.Trace.TraceWarning($"Paper Route: cover thumbnail failed: {e.GetType().Name}: {e.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best effort: the drive may already be gone.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort.
        }
    }
}
