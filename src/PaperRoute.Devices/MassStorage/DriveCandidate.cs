namespace PaperRoute.Devices.MassStorage;

/// <summary>A drive as seen by Windows, before deciding whether it is a Kindle.</summary>
/// <param name="Root">Root folder, for example <c>K:\</c>.</param>
/// <param name="Label">Volume label, if readable.</param>
/// <param name="Removable">Whether Windows reports the drive as removable.</param>
/// <param name="Ready">Whether the drive has media and can be read.</param>
/// <param name="FreeBytes">Free space in bytes.</param>
/// <param name="HardwareId">Plug and Play ID of the disk (vendor, product, serial), when Windows reports it.</param>
internal sealed record DriveCandidate(string Root, string? Label, bool Removable, bool Ready, long FreeBytes, string? HardwareId = null);
