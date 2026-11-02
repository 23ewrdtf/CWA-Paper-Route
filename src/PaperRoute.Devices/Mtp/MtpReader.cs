using System.Runtime.InteropServices;

using MediaDevices;

using PaperRoute.Delivery;

namespace PaperRoute.Devices.Mtp;

/// <summary>A Kindle connected over MTP. Experimental: not tested on real MTP hardware.</summary>
/// <param name="deviceId">Windows device ID, used to reconnect for each copy.</param>
/// <param name="friendlyName">Name shown in Windows.</param>
/// <param name="freeBytes">Free space when the device was found.</param>
public sealed class MtpReader(string deviceId, string? friendlyName, long freeBytes) : IReaderDevice
{
    /// <inheritdoc/>
    public string DisplayName { get; } = string.IsNullOrWhiteSpace(friendlyName) ? "Kindle" : friendlyName.Trim();

    /// <inheritdoc/>
    public DeviceKind Kind => DeviceKind.Mtp;

    /// <inheritdoc/>
    public long FreeBytes { get; } = freeBytes;

    /// <inheritdoc/>
    public void Write(string fileName, ReadOnlyMemory<byte> content)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        if (fileName.Length == 0 || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("Expected a plain file name without folders", nameof(fileName));
        }

        if (content.Length > FreeBytes)
        {
            throw new InsufficientSpaceException(content.Length, FreeBytes);
        }

        var devices = (MediaDeviceManager.Instance.GetDevices() ?? []).ToList();
        try
        {
            var device = devices.FirstOrDefault(d => string.Equals(d.DeviceId, deviceId, StringComparison.Ordinal))
                ?? throw new DeviceWriteException("the Kindle was disconnected");
            Copy(device, fileName, content);
        }
        finally
        {
            foreach (var d in devices)
            {
                d.Dispose();
            }
        }
    }

    private static void Copy(MediaDevice device, string fileName, ReadOnlyMemory<byte> content)
    {
        string? target = null;
        try
        {
            device.Connect(MediaDeviceAccess.Default, MediaDeviceShare.Read, false);
            var storage = MainStorage(device) ?? throw new DeviceWriteException("the Kindle has no storage");
            var root = storage.RootDirectory?.FullName ?? throw new DeviceWriteException("the Kindle storage has no root folder");
            var documents = root.TrimEnd('\\') + @"\documents";
            if (!device.DirectoryExists(documents))
            {
                device.CreateDirectory(documents);
            }

            // Upload under a temporary name first, so a failed re-send keeps the old copy.
            var final = documents + "\\" + fileName;
            target = final + ".paperroute-tmp";
            if (device.FileExists(target))
            {
                device.DeleteFile(target);
            }

            using (var stream = new MemoryStream(content.ToArray(), writable: false))
            {
                device.UploadFile(stream, target);
            }

            if ((long)device.GetFileInfo(target).Length != content.Length)
            {
                throw new IOException("the copied file has the wrong size");
            }

            if (device.FileExists(final))
            {
                device.DeleteFile(final);
            }

            device.Rename(target, fileName);
            target = null; // nothing left to clean up
        }
        catch (Exception e) when (e is COMException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            TryDelete(device, target);
            throw new DeviceWriteException(e.Message, e);
        }
        finally
        {
            if (device.IsConnected)
            {
                device.Disconnect();
            }
        }
    }

    internal static MediaDriveInfo? MainStorage(MediaDevice device)
    {
        var drives = device.GetDrives() ?? [];
        return drives.FirstOrDefault(d => d.Name?.Contains("Internal", StringComparison.OrdinalIgnoreCase) ?? false) ?? drives.FirstOrDefault();
    }

    private static void TryDelete(MediaDevice device, string? path)
    {
        if (path is null || !device.IsConnected)
        {
            return;
        }

        try
        {
            if (device.FileExists(path))
            {
                device.DeleteFile(path);
            }
        }
        catch (Exception e) when (e is COMException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Best effort: the device may already be gone.
        }
    }
}
