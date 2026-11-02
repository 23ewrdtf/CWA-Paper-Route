using System.Diagnostics.CodeAnalysis;

using PaperRoute.Delivery;

namespace PaperRoute.Devices;

/// <summary>Combines several locators; one failing locator never hides the others' readers.</summary>
public sealed class CompositeLocator : IDeviceLocator
{
    private readonly IDeviceLocator[] _inner;
    private readonly Action<string> _problem;

    /// <summary>Initializes a new instance of the <see cref="CompositeLocator"/> class.</summary>
    /// <param name="inner">Locators to combine.</param>
    public CompositeLocator(params IDeviceLocator[] inner)
        : this(_ => { }, inner)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CompositeLocator"/> class.</summary>
    /// <param name="problem">Receives a description when a locator fails.</param>
    /// <param name="inner">Locators to combine.</param>
    public CompositeLocator(Action<string> problem, params IDeviceLocator[] inner)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(inner);
        _problem = problem;
        _inner = inner;
    }

    /// <inheritdoc/>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Isolation boundary: a broken device driver stack (for example MTP/WPD COM errors) must not stop USB-drive Kindles from working.")]
    public IReadOnlyList<IReaderDevice> FindReaders()
    {
        var readers = new List<IReaderDevice>();
        foreach (var locator in _inner)
        {
            try
            {
                readers.AddRange(locator.FindReaders());
            }
            catch (Exception e)
            {
                _problem($"{locator.GetType().Name} failed: {e.Message}");
            }
        }

        return readers;
    }
}
