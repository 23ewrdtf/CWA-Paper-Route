using PaperRoute.Devices.Mtp;

namespace PaperRoute.Devices.Tests;

public sealed class KindleIdentityTests
{
    [Theory]
    [InlineData("Amazon", "Kindle Paperwhite", "Kindle", true)]
    [InlineData("Amazon", "Fire HD 10", "Fire", false)]
    [InlineData("Samsung", "Galaxy", "Galaxy S24", false)]
    [InlineData(null, null, "Kindle Scribe", true)]
    [InlineData("Kindle   ", "Internal Storage", "Kindle", true)]
    [InlineData("Generic-", "SD/MMC", "G:\\", false)]
    public void Identifies_kindles(string? manufacturer, string? model, string? friendlyName, bool expected) =>
        Assert.Equal(expected, KindleIdentity.IsKindle(manufacturer, model, friendlyName));

    [Theory]
    [InlineData(@"USBSTOR\DISK&VEN_KINDLE&PROD_INTERNAL_STORAGE&REV_0100\G000AB1234567890&0", true)] // Paperwhite (sample ID)
    [InlineData(@"USBSTOR\DISK&VEN_AMAZON&PROD_KINDLE&REV_0100\G000XX&0", true)]
    [InlineData(@"USBSTOR\DISK&VEN_SANDISK&PROD_CRUZER_BLADE&REV_1.00\4C530001&0", false)]
    [InlineData(@"USBSTOR\DISK&VEN_GENERIC-&PROD_SD#MMC&REV_1.00\058F84618461&1", false)]
    [InlineData(@"SCSI\DISK&VEN_KINDLEX&PROD_FAKE\1", false)] // VEN_ must match exactly
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Recognises_kindle_hardware(string? pnpDeviceId, bool expected) =>
        Assert.Equal(expected, KindleIdentity.IsKindleHardware(pnpDeviceId));

    [Theory]
    [InlineData("MTP: 1.00", @"\\?\usb#vid_1949&pid_9981#g000xx0000000000#{6ac27878-a6fa-4155-ba85-f98f491d4f33}", true)]
    [InlineData("MTP", @"\\?\usb#vid_1949&pid_9981#abc#{6ac27878-a6fa-4155-ba85-f98f491d4f33}", true)]
    // Owner's Paperwhite: a USB drive that Windows also lists as a portable device.
    [InlineData("MSC:", @"\\?\swd#wpdbusenum#_??_usbstor#disk&ven_kindle&prod_internal_storage&rev_0100#G000AB1234567890&0#{53f56307-b6bf-11d0-94f2-00a0c91efb8b}#{6ac27878-a6fa-4155-ba85-f98f491d4f33}", false)]
    [InlineData("MTP: 1.00", @"\\?\swd#wpdbusenum#_??_usbstor#disk&ven_kindle#x", false)]
    [InlineData(null, @"\\?\usb#vid_1949&pid_9981#abc", false)]
    [InlineData("", @"\\?\usb#vid_1949&pid_9981#abc", false)]
    public void Only_real_mtp_devices_count(string? protocol, string deviceId, bool expected) =>
        Assert.Equal(expected, KindleIdentity.IsMtpDevice(protocol, deviceId));
}
