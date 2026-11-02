namespace PaperRoute.Delivery;

/// <summary>Copying a book to the reader failed.</summary>
public sealed class DeviceWriteException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DeviceWriteException"/> class.</summary>
    public DeviceWriteException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DeviceWriteException"/> class.</summary>
    /// <param name="message">Plain-language reason, shown to the user.</param>
    public DeviceWriteException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DeviceWriteException"/> class.</summary>
    /// <param name="message">Plain-language reason, shown to the user.</param>
    /// <param name="innerException">The underlying error.</param>
    public DeviceWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
