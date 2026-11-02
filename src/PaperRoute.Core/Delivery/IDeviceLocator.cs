namespace PaperRoute.Delivery;

/// <summary>Finds connected e-readers.</summary>
public interface IDeviceLocator
{
    /// <summary>Returns the e-readers connected right now.</summary>
    /// <returns>Connected readers; empty when none.</returns>
    IReadOnlyList<IReaderDevice> FindReaders();
}
