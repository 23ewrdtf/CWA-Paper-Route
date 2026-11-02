using PaperRoute.Delivery;

namespace PaperRoute.Devices.Tests;

public sealed class CompositeLocatorTests
{
    [Fact]
    public void Concatenates_readers_from_all_locators() =>
        Assert.Equal(3, new CompositeLocator(new FakeLocator(1), new FakeLocator(2)).FindReaders().Count);

    [Fact]
    public void Skips_a_throwing_locator()
    {
        var problems = new List<string>();

        var readers = new CompositeLocator(problems.Add, new ThrowingLocator(), new FakeLocator(1)).FindReaders();

        Assert.Single(readers);
        Assert.Contains(problems, p => p.Contains("WPD exploded", StringComparison.Ordinal));
    }

    private sealed class FakeLocator(int count) : IDeviceLocator
    {
        public IReadOnlyList<IReaderDevice> FindReaders() => [.. Enumerable.Range(0, count).Select(_ => new FakeReader())];
    }

    private sealed class ThrowingLocator : IDeviceLocator
    {
        public IReadOnlyList<IReaderDevice> FindReaders() => throw new InvalidOperationException("WPD exploded");
    }

    private sealed class FakeReader : IReaderDevice
    {
        public string DisplayName => "Kindle";

        public DeviceKind Kind => DeviceKind.Mtp;

        public long FreeBytes => long.MaxValue;

        public void Write(string fileName, ReadOnlyMemory<byte> content)
        {
        }
    }
}
