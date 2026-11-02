namespace PaperRoute.Config;

/// <summary>The configuration file or a setting in it is invalid.</summary>
public sealed class ConfigException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ConfigException"/> class.</summary>
    public ConfigException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ConfigException"/> class.</summary>
    /// <param name="message">Plain-language description of the problem.</param>
    public ConfigException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ConfigException"/> class.</summary>
    /// <param name="message">Plain-language description of the problem.</param>
    /// <param name="innerException">The underlying error.</param>
    public ConfigException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
