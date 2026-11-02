using PaperRoute.Delivery;

namespace PaperRoute.Core.Tests.Fakes;

internal sealed class FakeLocator(params IReaderDevice[] devices) : IDeviceLocator
{
    public IReadOnlyList<IReaderDevice> FindReaders() => devices;
}
