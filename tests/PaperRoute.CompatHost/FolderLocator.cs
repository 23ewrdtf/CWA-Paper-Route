using PaperRoute.Delivery;

namespace PaperRoute.CompatHost;

internal sealed class FolderLocator(string dir) : IDeviceLocator
{
    public IReadOnlyList<IReaderDevice> FindReaders() => [new FolderDevice(dir)];
}
