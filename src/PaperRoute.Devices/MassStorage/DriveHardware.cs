using System.Management;
using System.Runtime.InteropServices;

namespace PaperRoute.Devices.MassStorage;

/// <summary>Asks Windows which physical device is behind a drive letter.</summary>
internal static class DriveHardware
{
    /// <summary>
    /// Returns the Plug and Play ID of the disk behind a drive, for example
    /// <c>USBSTOR\DISK&amp;VEN_KINDLE&amp;PROD_INTERNAL_STORAGE&amp;REV_0100\G000AB1234567890&amp;0</c>
    /// (vendor, product and serial number), or <see langword="null"/> when Windows can't tell.
    /// </summary>
    /// <param name="root">Drive root such as <c>K:\</c>.</param>
    /// <returns>The device ID, or <see langword="null"/>.</returns>
    public static string? TryGetPnpDeviceId(string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        // Only "X:\" goes into the WMI query.
        if (root.Length < 2 || !char.IsAsciiLetter(root[0]) || root[1] != ':')
        {
            return null;
        }

        var letter = char.ToUpperInvariant(root[0]) + ":";
        try
        {
            using var partitions = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{letter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");
            foreach (var partition in partitions.Get().Cast<ManagementObject>())
            {
                using (partition)
                {
                    var partitionId = partition["DeviceID"] as string;
                    if (partitionId is null || partitionId.Contains('\'', StringComparison.Ordinal))
                    {
                        continue;
                    }

                    using var disks = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionId}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                    foreach (var disk in disks.Get().Cast<ManagementObject>())
                    {
                        using (disk)
                        {
                            if (disk["PNPDeviceID"] is string id && id.Length > 0)
                            {
                                return id;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e) when (e is ManagementException or COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            // WMI unavailable or the drive vanished: fall back to the label/folder check.
        }

        return null;
    }
}
