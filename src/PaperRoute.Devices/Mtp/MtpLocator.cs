using System.Runtime.InteropServices;

using MediaDevices;

using PaperRoute.Delivery;

namespace PaperRoute.Devices.Mtp;

/// <summary>Finds Kindles connected over MTP (newer models). Experimental: not tested on real MTP hardware.</summary>
public sealed class MtpLocator : IDeviceLocator
{
    /// <inheritdoc/>
    public IReadOnlyList<IReaderDevice> FindReaders()
    {
        var readers = new List<IReaderDevice>();
        foreach (var device in MediaDeviceManager.Instance.GetDevices() ?? [])
        {
            using (device)
            {
                if (!KindleIdentity.IsKindle(device.Manufacturer, device.Description, device.FriendlyName))
                {
                    continue;
                }

                try
                {
                    device.Connect(MediaDeviceAccess.GenericRead, MediaDeviceShare.Read, false);
                    if (KindleIdentity.IsMtpDevice(device.Protocol, device.DeviceId))
                    {
                        var storage = MtpReader.MainStorage(device);
                        if (storage is not null)
                        {
                            readers.Add(new MtpReader(device.DeviceId, device.FriendlyName, storage.AvailableFreeSpace));
                        }
                    }
                }
                catch (Exception e) when (e is COMException or IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    // Device unplugged or busy while probing: treat as not connected.
                }
                finally
                {
                    if (device.IsConnected)
                    {
                        device.Disconnect();
                    }
                }
            }
        }

        return readers;
    }
}
