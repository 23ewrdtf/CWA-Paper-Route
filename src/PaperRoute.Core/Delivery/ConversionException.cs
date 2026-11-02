namespace PaperRoute.Delivery;

/// <summary>Converting a book failed.</summary>
public sealed class ConversionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ConversionException"/> class.</summary>
    public ConversionException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ConversionException"/> class.</summary>
    /// <param name="message">Plain-language reason, shown to the user.</param>
    public ConversionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ConversionException"/> class.</summary>
    /// <param name="message">Plain-language reason, shown to the user.</param>
    /// <param name="innerException">The underlying error.</param>
    public ConversionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
