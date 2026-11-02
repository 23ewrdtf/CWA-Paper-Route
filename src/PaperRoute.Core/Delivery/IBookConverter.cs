namespace PaperRoute.Delivery;

/// <summary>Converts EPUB books to a format Kindles can read over USB.</summary>
public interface IBookConverter
{
    /// <summary>Gets a value indicating whether the converter can be used (for example, Calibre is installed).</summary>
    bool IsAvailable { get; }

    /// <summary>Converts an EPUB to AZW3.</summary>
    /// <param name="epub">The EPUB book.</param>
    /// <returns>The AZW3 book, named like the EPUB with an <c>.azw3</c> extension.</returns>
    /// <exception cref="ConversionException">The conversion failed or took too long.</exception>
    BookAttachment ToAzw3(BookAttachment epub);
}
