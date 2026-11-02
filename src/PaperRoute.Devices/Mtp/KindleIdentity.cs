namespace PaperRoute.Devices.Mtp;

/// <summary>Recognises Kindles among Windows portable devices.</summary>
public static class KindleIdentity
{
    private const string Kindle = "Kindle";

    /// <summary>Returns whether the device looks like an Amazon Kindle.</summary>
    /// <param name="manufacturer">Manufacturer reported by the device.</param>
    /// <param name="model">Model or description reported by the device.</param>
    /// <param name="friendlyName">Name shown in Windows.</param>
    /// <returns><see langword="true"/> for Kindles; Fire tablets and other devices are excluded.</returns>
    public static bool IsKindle(string? manufacturer, string? model, string? friendlyName)
    {
        var name = friendlyName?.Trim() ?? string.Empty;
        if (name.StartsWith(Kindle, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var maker = manufacturer?.Trim() ?? string.Empty;
        var isAmazon = maker.Contains("Amazon", StringComparison.OrdinalIgnoreCase) || maker.Equals(Kindle, StringComparison.OrdinalIgnoreCase);
        return isAmazon
            && ((model?.Contains(Kindle, StringComparison.OrdinalIgnoreCase) ?? false) || name.Contains(Kindle, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns whether a disk's Plug and Play ID says it is a Kindle (vendor KINDLE or AMAZON).</summary>
    /// <param name="pnpDeviceId">For example <c>USBSTOR\DISK&amp;VEN_KINDLE&amp;PROD_INTERNAL_STORAGE&amp;REV_0100\G000AB1234567890&amp;0</c>.</param>
    /// <returns><see langword="true"/> for Kindle hardware.</returns>
    public static bool IsKindleHardware(string? pnpDeviceId) =>
        pnpDeviceId is not null
        && pnpDeviceId.Split(['\\', '&'], StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.Equals("VEN_KINDLE", StringComparison.OrdinalIgnoreCase) || part.Equals("VEN_AMAZON", StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns whether a portable device is a real MTP device.</summary>
    /// <remarks>
    /// Windows also lists USB drives as portable devices (protocol <c>MSC:</c>, ID containing <c>usbstor</c>).
    /// A Kindle connected as a drive would otherwise be found twice: once as a drive, once here.
    /// </remarks>
    /// <param name="protocol">Protocol reported by the device, for example <c>MTP: 1.00</c>.</param>
    /// <param name="deviceId">Windows device ID.</param>
    /// <returns><see langword="true"/> only for MTP devices that are not USB drives.</returns>
    public static bool IsMtpDevice(string? protocol, string? deviceId) =>
        protocol is not null
        && protocol.StartsWith("MTP", StringComparison.OrdinalIgnoreCase)
        && !(deviceId?.Contains("usbstor", StringComparison.OrdinalIgnoreCase) ?? false);
}
