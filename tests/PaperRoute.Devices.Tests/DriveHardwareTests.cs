using PaperRoute.Devices.MassStorage;
using PaperRoute.Devices.Mtp;

namespace PaperRoute.Devices.Tests;

public sealed class DriveHardwareTests
{
    [Fact]
    public void Real_drives_report_hardware_and_a_connected_kindle_is_recognised()
    {
        // Read-only, against this PC's real removable drives. On a PC with a Kindle plugged in as a drive,
        // Windows must report it as Kindle hardware.
        var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable && d.IsReady).ToList();
        Assert.SkipWhen(drives.Count == 0, "No removable drives on this machine");

        foreach (var drive in drives)
        {
            var id = DriveHardware.TryGetPnpDeviceId(drive.RootDirectory.FullName);
            TestContext.Current.SendDiagnosticMessage($"{drive.Name} {drive.VolumeLabel}: {id}");
            if (string.Equals(drive.VolumeLabel, "Kindle", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(KindleIdentity.IsKindleHardware(id), $"{drive.Name} is labelled Kindle but hardware id is '{id}'");
            }
        }
    }

    [Fact]
    public void Unknown_drive_has_no_hardware_id() =>
        Assert.Null(DriveHardware.TryGetPnpDeviceId(@"Q:\"));
}
