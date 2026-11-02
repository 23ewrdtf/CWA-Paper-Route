using PaperRoute.Delivery;
using PaperRoute.Devices.Mtp;

namespace PaperRoute.Devices.MassStorage;

/// <summary>Finds Kindles connected as USB drives (drive letter with a <c>documents</c> folder).</summary>
public sealed class MassStorageLocator : IDeviceLocator
{
    private const string KindleLabel = "Kindle";

    private readonly Func<IEnumerable<DriveCandidate>> _drives;

    /// <summary>Initializes a new instance of the <see cref="MassStorageLocator"/> class that reads the PC's removable drives.</summary>
    public MassStorageLocator()
        : this(RemovableDrives)
    {
    }

    internal MassStorageLocator(Func<IEnumerable<DriveCandidate>> drives) => _drives = drives;

    /// <inheritdoc/>
    public IReadOnlyList<IReaderDevice> FindReaders() =>
        [.. _drives().Where(IsKindle).Select(d => new MassStorageReader(d.Root, d.FreeBytes, () => StillSameKindle(d)))];

    // Hardware ID (from Windows) decides when known; the label/folder layout is only a fallback.
    private static bool IsKindle(DriveCandidate drive) =>
        drive.Ready && drive.Removable
        && (drive.HardwareId is not null
            ? KindleIdentity.IsKindleHardware(drive.HardwareId)
            : string.Equals(drive.Label, KindleLabel, StringComparison.OrdinalIgnoreCase)
              || (Directory.Exists(Path.Join(drive.Root, "documents")) && Directory.Exists(Path.Join(drive.Root, "system"))));

    // Re-checked just before writing: the drive letter may now belong to another device. The hardware ID
    // includes the serial number, so even another Kindle on the same letter is refused.
    private bool StillSameKindle(DriveCandidate found) =>
        _drives().Any(d => string.Equals(d.Root, found.Root, StringComparison.OrdinalIgnoreCase)
            && IsKindle(d)
            && string.Equals(d.HardwareId, found.HardwareId, StringComparison.OrdinalIgnoreCase));


    private static IEnumerable<DriveCandidate> RemovableDrives()
    {
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable))
        {
            DriveCandidate candidate;
            try
            {
                candidate = drive.IsReady
                    ? new DriveCandidate(drive.RootDirectory.FullName, drive.VolumeLabel, Removable: true, Ready: true, drive.AvailableFreeSpace, DriveHardware.TryGetPnpDeviceId(drive.RootDirectory.FullName))
                    : new DriveCandidate(drive.RootDirectory.FullName, Label: null, Removable: true, Ready: false, FreeBytes: 0);
            }
            catch (IOException)
            {
                continue; // Card reader slot emptied or drive removed while looking.
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            yield return candidate;
        }
    }
}
