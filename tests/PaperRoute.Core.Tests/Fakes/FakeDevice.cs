using PaperRoute.Delivery;

namespace PaperRoute.Core.Tests.Fakes;

internal sealed class FakeDevice(string displayName) : IReaderDevice
{
    private readonly Lock _gate = new();
    private readonly List<string> _written = [];
    private int _active;

    public string DisplayName { get; } = displayName;

    public DeviceKind Kind => DeviceKind.MassStorage;

    public long FreeBytes { get; init; } = long.MaxValue;

    public TimeSpan WriteDelay { get; init; }

    public Exception? ThrowOnWrite { get; init; }

    public bool SawOverlap { get; private set; }

    public IReadOnlyList<string> Written
    {
        get
        {
            lock (_gate)
            {
                return [.. _written];
            }
        }
    }

    public void Write(string fileName, ReadOnlyMemory<byte> content)
    {
        if (Interlocked.Increment(ref _active) > 1)
        {
            SawOverlap = true;
        }

        try
        {
            if (content.Length > FreeBytes)
            {
                throw new InsufficientSpaceException(content.Length, FreeBytes);
            }

            if (ThrowOnWrite is not null)
            {
                throw ThrowOnWrite;
            }

            if (WriteDelay > TimeSpan.Zero)
            {
                Thread.Sleep(WriteDelay);
            }

            lock (_gate)
            {
                _written.Add(fileName);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }
}
