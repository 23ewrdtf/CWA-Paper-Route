namespace PaperRoute.Delivery;

/// <summary>The reader does not have room for the book.</summary>
public sealed class InsufficientSpaceException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="InsufficientSpaceException"/> class.</summary>
    public InsufficientSpaceException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InsufficientSpaceException"/> class.</summary>
    /// <param name="message">Description of the problem.</param>
    public InsufficientSpaceException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InsufficientSpaceException"/> class.</summary>
    /// <param name="message">Description of the problem.</param>
    /// <param name="innerException">The underlying error.</param>
    public InsufficientSpaceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InsufficientSpaceException"/> class.</summary>
    /// <param name="neededBytes">Size of the book.</param>
    /// <param name="freeBytes">Free space on the reader.</param>
    public InsufficientSpaceException(long neededBytes, long freeBytes)
        : base($"{neededBytes} bytes needed, {freeBytes} bytes free")
    {
        NeededBytes = neededBytes;
        FreeBytes = freeBytes;
    }

    /// <summary>Gets the size of the book in bytes.</summary>
    public long NeededBytes { get; }

    /// <summary>Gets the free space on the reader in bytes.</summary>
    public long FreeBytes { get; }
}
