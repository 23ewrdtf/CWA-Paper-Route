using PaperRoute.Delivery;
using PaperRoute.Devices.MassStorage;

namespace PaperRoute.Devices.Tests;

public sealed class MassStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "paperroute-drive-" + Guid.NewGuid().ToString("N"));

    public MassStorageTests() => Directory.CreateDirectory(_root);

    private string Documents => Path.Combine(_root, "documents");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Finds_drive_labelled_Kindle() =>
        Assert.Single(Locator(Candidate(label: "Kindle")).FindReaders());

    [Fact]
    public void Label_match_ignores_case() =>
        Assert.Single(Locator(Candidate(label: "KINDLE")).FindReaders());

    [Fact]
    public void Finds_unlabelled_drive_with_documents_and_system() =>
        Assert.Single(Locator(Candidate(label: "NO NAME", dirs: ["documents", "system"])).FindReaders());

    [Fact]
    public void Usb_stick_labelled_Kindle_is_ignored_when_hardware_is_known() =>
        Assert.Empty(Locator(Candidate(label: "Kindle", dirs: ["documents", "system"]) with { HardwareId = SanDisk }).FindReaders());

    [Fact]
    public void Kindle_hardware_is_found_whatever_its_label() =>
        Assert.Single(Locator(Candidate(label: "MY BOOKS") with { HardwareId = Paperwhite }).FindReaders());

    [Fact]
    public void Another_kindle_on_the_same_letter_is_not_written()
    {
        var calls = 0;
        var first = Candidate(label: "Kindle") with { HardwareId = Paperwhite };
        var second = first with { HardwareId = Paperwhite.Replace("G000AB1234567890", "1111111111111111", StringComparison.Ordinal) };
        var reader = Assert.Single(new MassStorageLocator(() => calls++ == 0 ? [first] : [second]).FindReaders());

        Assert.Throws<DeviceWriteException>(() => reader.Write("a.azw3", new byte[10]));
        Assert.False(Directory.Exists(Documents));
    }

    [Fact]
    public void Same_kindle_is_written()
    {
        var kindle = Candidate(label: "Kindle") with { HardwareId = Paperwhite };
        var reader = Assert.Single(new MassStorageLocator(() => [kindle]).FindReaders());

        reader.Write("a.azw3", new byte[10]);

        Assert.True(File.Exists(Path.Combine(Documents, "a.azw3")));
    }

    [Fact]
    public void Ignores_ordinary_usb_stick() =>
        Assert.Empty(Locator(Candidate(label: "USB", dirs: ["documents"])).FindReaders());

    [Fact]
    public void Ignores_camera_card() =>
        Assert.Empty(Locator(Candidate(label: "EOS_DIGITAL", dirs: ["DCIM"])).FindReaders());

    [Fact]
    public void Ignores_drive_that_is_not_ready() =>
        Assert.Empty(Locator(Candidate(label: "Kindle") with { Ready = false }).FindReaders());

    [Fact]
    public void Ignores_fixed_disks() =>
        Assert.Empty(Locator(Candidate(label: "Kindle") with { Removable = false }).FindReaders());

    [Fact]
    public void Display_name_shows_drive_letter()
    {
        var reader = new MassStorageLocator(() => [new DriveCandidate(@"K:\", "Kindle", Removable: true, Ready: true, FreeBytes: 1)]).FindReaders();

        Assert.Equal("Kindle (K:)", Assert.Single(reader).DisplayName);
    }

    [Fact]
    public void Reports_free_space_and_kind()
    {
        var reader = Assert.Single(Locator(Candidate(label: "Kindle", freeBytes: 1234)).FindReaders());

        Assert.Equal(1234, reader.FreeBytes);
        Assert.Equal(DeviceKind.MassStorage, reader.Kind);
    }

    [Fact]
    public void Creates_documents_folder_when_missing()
    {
        Reader().Write("a.azw3", new byte[10]);

        Assert.True(File.Exists(Path.Combine(Documents, "a.azw3")));
    }

    [Fact]
    public void Writes_into_documents_and_overwrites()
    {
        var reader = Reader();

        reader.Write("a.azw3", new byte[10]);
        reader.Write("a.azw3", new byte[20]);

        Assert.Equal(20, new FileInfo(Path.Combine(Documents, "a.azw3")).Length);
        Assert.Single(Directory.GetFiles(Documents));
    }

    [Fact]
    public void Content_is_written_exactly()
    {
        byte[] content = [1, 2, 3, 4, 5];

        Reader().Write("a.mobi", content);

        Assert.Equal(content, File.ReadAllBytes(Path.Combine(Documents, "a.mobi")));
    }

    [Fact]
    public void Write_failure_leaves_nothing_behind()
    {
        var reader = Reader();
        File.WriteAllText(Documents, "a file where the documents folder should be");

        Assert.Throws<DeviceWriteException>(() => reader.Write("a.azw3", new byte[10]));

        Assert.Empty(Directory.GetFiles(_root, "*.paperroute-tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Locked_target_fails_and_keeps_old_file()
    {
        var reader = Reader();
        reader.Write("a.azw3", new byte[10]);
        var target = Path.Combine(Documents, "a.azw3");

        using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Throws<DeviceWriteException>(() => reader.Write("a.azw3", new byte[20]));
        }

        Assert.Equal(10, new FileInfo(target).Length);
        Assert.Empty(Directory.GetFiles(Documents, "*.paperroute-tmp"));
    }

    [Fact]
    public void Drive_that_is_no_longer_a_kindle_is_not_written()
    {
        var calls = 0;
        var kindle = Candidate(label: "Kindle");
        var otherStick = kindle with { Label = "USB STICK" }; // same letter, different device
        var locator = new MassStorageLocator(() => calls++ == 0 ? [kindle] : [otherStick]);
        var reader = Assert.Single(locator.FindReaders());

        var error = Assert.Throws<DeviceWriteException>(() => reader.Write("a.azw3", new byte[10]));

        Assert.Contains("no longer connected", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Documents));
    }

    [Fact]
    public void Not_enough_space_throws_before_writing()
    {
        var reader = Reader(freeBytes: 5);

        var error = Assert.Throws<InsufficientSpaceException>(() => reader.Write("a.azw3", new byte[10]));

        Assert.Equal(10, error.NeededBytes);
        Assert.Equal(5, error.FreeBytes);
        Assert.False(Directory.Exists(Documents) && Directory.EnumerateFileSystemEntries(Documents).Any());
    }

    [Fact]
    public void Scanning_the_real_drives_never_throws()
    {
        // Runs against this PC's drives (CI: none; developer PC: may include a real Kindle). Read-only.
        var readers = new MassStorageLocator().FindReaders();

        foreach (var reader in readers)
        {
            TestContext.Current.SendDiagnosticMessage($"Found {reader.DisplayName}, {reader.FreeBytes / (1024 * 1024)} MB free");
        }

        Assert.All(readers, r => Assert.StartsWith("Kindle", r.DisplayName, StringComparison.Ordinal));
    }

    private const string Paperwhite = @"USBSTOR\DISK&VEN_KINDLE&PROD_INTERNAL_STORAGE&REV_0100\G000AB1234567890&0";
    private const string SanDisk = @"USBSTOR\DISK&VEN_SANDISK&PROD_CRUZER_BLADE&REV_1.00\4C530001&0";

    private static MassStorageLocator Locator(DriveCandidate candidate) => new(() => [candidate]);

    private DriveCandidate Candidate(string? label, string[]? dirs = null, long freeBytes = long.MaxValue)
    {
        foreach (var dir in dirs ?? [])
        {
            Directory.CreateDirectory(Path.Combine(_root, dir));
        }

        return new DriveCandidate(_root, label, Removable: true, Ready: true, FreeBytes: freeBytes);
    }

    private IReaderDevice Reader(long freeBytes = long.MaxValue) => Assert.Single(Locator(Candidate("Kindle", freeBytes: freeBytes)).FindReaders());
}
