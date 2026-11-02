namespace PaperRoute.Delivery;

/// <summary>A connected e-reader that books can be copied to.</summary>
public interface IReaderDevice
{
    /// <summary>Gets the name shown to the user, for example <c>Kindle (E:)</c>.</summary>
    string DisplayName { get; }

    /// <summary>Gets how the reader is connected.</summary>
    DeviceKind Kind { get; }

    /// <summary>Gets the free space on the reader in bytes.</summary>
    long FreeBytes { get; }

    /// <summary>Copies a book into the reader's <c>documents</c> folder, replacing a file with the same name.</summary>
    /// <param name="fileName">Sanitized file name.</param>
    /// <param name="content">Book contents.</param>
    /// <exception cref="InsufficientSpaceException">The book does not fit; nothing was written.</exception>
    /// <exception cref="DeviceWriteException">The copy failed; nothing was left behind.</exception>
    void Write(string fileName, ReadOnlyMemory<byte> content);
}
