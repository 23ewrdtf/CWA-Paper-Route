using System.Net;

using MimeKit;

using PaperRoute.Core.Tests.Fakes;
using PaperRoute.Delivery;

namespace PaperRoute.Core.Tests;

public sealed class DeliveryServiceTests
{
    [Fact]
    public void No_attachment_is_test_message() =>
        AssertOutcome(Deliver(TestMessages.BodyOnly(), readers: 1), 250, "Test e-mail received - CWA is connected", OutcomeKind.TestMessage);

    [Fact]
    public void No_kindle_rejects() =>
        AssertOutcome(Deliver(TestMessages.Book("a.azw3"), readers: 0), 550, "Kindle not connected - plug it in and send again", OutcomeKind.Rejected);

    [Fact]
    public void Two_kindles_reject() =>
        AssertOutcome(Deliver(TestMessages.Book("a.azw3"), readers: 2), 550, "More than one Kindle connected - unplug all but one", OutcomeKind.Rejected);

    [Fact]
    public void Two_books_rejected() =>
        AssertOutcome(Deliver(TestMessages.Books(("a.azw3", 10), ("b.azw3", 10)), readers: 1), 550, "Only one book per message", OutcomeKind.Rejected);

    [Fact]
    public void Epub_without_calibre_asks_to_install_it() =>
        AssertOutcome(Deliver(TestMessages.Book("a.epub"), readers: 1), 550, "Install Calibre (calibre-ebook.com) so Paper Route can convert EPUB books", OutcomeKind.Rejected);

    [Fact]
    public void Uppercase_epub_without_calibre_asks_to_install_it() =>
        AssertOutcome(Deliver(TestMessages.Book("A.EPUB"), readers: 1), 550, "Install Calibre (calibre-ebook.com) so Paper Route can convert EPUB books", OutcomeKind.Rejected);

    [Fact]
    public void Epub_is_converted_and_delivered_as_azw3()
    {
        var device = new FakeDevice("Kindle (K:)");
        var converter = new FakeConverter();

        var outcome = Deliver(TestMessages.Book("Dune.epub"), device: device, converter: converter);

        AssertOutcome(outcome, 250, "Delivered Dune.azw3 to Kindle (K:)", OutcomeKind.Delivered);
        Assert.Equal(["Dune.azw3"], device.Written);
        Assert.Equal(1, converter.Calls);
    }

    [Fact]
    public void Conversion_failure_is_451()
    {
        var converter = new FakeConverter { Failure = "Calibre exited with code 3" };

        AssertOutcome(
            Deliver(TestMessages.Book("Dune.epub"), readers: 1, converter: converter),
            451,
            "Converting Dune.epub to AZW3 failed: Calibre exited with code 3 - nothing was copied",
            OutcomeKind.Failed);
    }

    [Fact]
    public void No_kindle_skips_the_slow_conversion()
    {
        var converter = new FakeConverter();

        AssertOutcome(Deliver(TestMessages.Book("Dune.epub"), readers: 0, converter: converter), 550, "Kindle not connected - plug it in and send again", OutcomeKind.Rejected);
        Assert.Equal(0, converter.Calls);
    }

    [Fact]
    public void Fake_epub_is_refused_before_conversion()
    {
        var converter = new FakeConverter();

        AssertOutcome(Deliver(TestMessages.Raw("a.epub", new byte[100]), readers: 1, converter: converter), 550, "This doesn't look like a real EPUB file - nothing was copied", OutcomeKind.Rejected);
        Assert.Equal(0, converter.Calls);
    }

    [Fact]
    public void Converter_output_is_still_checked()
    {
        var converter = new FakeConverter { Output = new byte[100] };

        AssertOutcome(Deliver(TestMessages.Book("a.epub"), readers: 1, converter: converter), 550, "This doesn't look like a real AZW3 file - nothing was copied", OutcomeKind.Rejected);
    }

    [Fact]
    public void Fake_azw3_is_refused_before_device_lookup() =>
        AssertOutcome(
            Deliver(TestMessages.Raw("a.azw3", new byte[2048]), readers: 0),
            550,
            "This doesn't look like a real AZW3 file - nothing was copied",
            OutcomeKind.Rejected);

    [Fact]
    public void Renamed_exe_is_refused_and_not_written()
    {
        var device = new FakeDevice("Kindle");
        var exe = new byte[2048];
        exe[0] = (byte)'M';
        exe[1] = (byte)'Z';

        var outcome = Deliver(TestMessages.Raw("holiday.pdf", exe), device: device);

        AssertOutcome(outcome, 550, "This doesn't look like a real PDF file - nothing was copied", OutcomeKind.Rejected);
        Assert.Empty(device.Written);
    }

    [Fact]
    public void Docx_is_not_a_book_anymore() =>
        AssertOutcome(Deliver(TestMessages.Book("letter.docx"), readers: 1), 550, "No e-book attachment found", OutcomeKind.Rejected);

    [Fact]
    public void Only_non_books_rejects() =>
        AssertOutcome(Deliver(TestMessages.Book("cover.jpg"), readers: 1), 550, "No e-book attachment found", OutcomeKind.Rejected);

    [Fact]
    public void Book_plus_cover_image_delivers_only_the_book()
    {
        var device = new FakeDevice("Kindle");

        var outcome = Deliver(TestMessages.Books(("a.azw3", 10), ("cover.jpg", 10)), device: device);

        AssertOutcome(outcome, 250, "Delivered a.azw3 to Kindle", OutcomeKind.Delivered);
        Assert.Equal(["a.azw3"], device.Written);
    }

    [Fact]
    public void Write_failure_is_451_with_reason() =>
        AssertOutcome(
            Deliver(TestMessages.Book("a.azw3"), device: new FakeDevice("Kindle") { ThrowOnWrite = new DeviceWriteException("device removed") }),
            451,
            "Copy to Kindle failed: device removed - nothing was saved, send again",
            OutcomeKind.Failed);

    [Fact]
    public void Full_device_is_550_space() =>
        AssertOutcome(
            Deliver(TestMessages.Book("a.azw3", bytes: 3_000_000), device: new FakeDevice("Kindle") { FreeBytes = 1_000_000 }),
            550,
            "Not enough space on Kindle: 3 MB needed, 1 MB free",
            OutcomeKind.Rejected);

    [Fact]
    public void Success_writes_then_250()
    {
        var device = new FakeDevice("Kindle (E:)");

        var outcome = Deliver(TestMessages.Book("a.azw3"), device: device);

        AssertOutcome(outcome, 250, "Delivered a.azw3 to Kindle (E:)", OutcomeKind.Delivered);
        Assert.Equal(["a.azw3"], device.Written);
    }

    [Fact]
    public async Task Concurrent_deliveries_are_serialized()
    {
        var device = new FakeDevice("Kindle") { WriteDelay = TimeSpan.FromMilliseconds(200) };
        var service = new DeliveryService(new FakeLocator(device), TestConfig.Instance, new MemoryLog());

        await Task.WhenAll(
            Task.Run(() => service.Deliver(TestMessages.Book("a.azw3"), IPAddress.Loopback), TestContext.Current.CancellationToken),
            Task.Run(() => service.Deliver(TestMessages.Book("b.azw3"), IPAddress.Loopback), TestContext.Current.CancellationToken));

        Assert.False(device.SawOverlap);
        Assert.Equal(2, device.Written.Count);
    }

    [Fact]
    public async Task Services_sharing_a_gate_never_copy_at_the_same_time()
    {
        // During a port change two listeners (two services) can be active; they must share one gate.
        var device = new FakeDevice("Kindle") { WriteDelay = TimeSpan.FromMilliseconds(200) };
        var gate = new Lock();
        var first = new DeliveryService(new FakeLocator(device), TestConfig.Instance, new MemoryLog(), converter: null, gate);
        var second = new DeliveryService(new FakeLocator(device), TestConfig.Instance, new MemoryLog(), converter: null, gate);

        await Task.WhenAll(
            Task.Run(() => first.Deliver(TestMessages.Book("a.azw3"), IPAddress.Loopback), TestContext.Current.CancellationToken),
            Task.Run(() => second.Deliver(TestMessages.Book("b.azw3"), IPAddress.Loopback), TestContext.Current.CancellationToken));

        Assert.False(device.SawOverlap);
        Assert.Equal(2, device.Written.Count);
    }

    [Fact]
    public void Every_outcome_is_logged()
    {
        var log = new MemoryLog();

        _ = Deliver(TestMessages.Book("a.azw3"), readers: 0, log: log);

        var line = Assert.Single(log.Lines);
        Assert.Contains("a.azw3", line, StringComparison.Ordinal);
        Assert.Contains("550", line, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Log_never_contains_password()
    {
        var log = new MemoryLog();

        _ = Deliver(TestMessages.Book("a.azw3"), readers: 1, log: log);
        _ = Deliver(TestMessages.BodyOnly(), readers: 1, log: log);

        Assert.DoesNotContain(log.Lines, l => l.Contains(TestConfig.AuthPassword, StringComparison.Ordinal));
    }

    private static DeliveryOutcome Deliver(MimeMessage message, int readers = 1, FakeDevice? device = null, MemoryLog? log = null, FakeConverter? converter = null)
    {
        IReaderDevice[] devices = device is not null
            ? [device]
            : [.. Enumerable.Range(0, readers).Select(_ => new FakeDevice("Kindle"))];
        return new DeliveryService(new FakeLocator(devices), TestConfig.Instance, log ?? new MemoryLog(), converter)
            .Deliver(message, IPAddress.Loopback);
    }

    private static void AssertOutcome(DeliveryOutcome outcome, int code, string text, OutcomeKind kind)
    {
        Assert.Equal(code, outcome.SmtpCode);
        Assert.Equal(text, outcome.Text);
        Assert.Equal(kind, outcome.Kind);
    }
}
