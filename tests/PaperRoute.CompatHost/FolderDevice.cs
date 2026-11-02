using PaperRoute.Delivery;

namespace PaperRoute.CompatHost;

internal sealed class FolderDevice(string dir) : IReaderDevice
{
    public string DisplayName => "Kindle";

    public DeviceKind Kind => DeviceKind.MassStorage;

    public long FreeBytes => long.MaxValue;

    public void Write(string fileName, ReadOnlyMemory<byte> content) => File.WriteAllBytes(Path.Combine(dir, fileName), content.ToArray());
}
