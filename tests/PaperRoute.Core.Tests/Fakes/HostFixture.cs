using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using MailKit.Net.Smtp;
using MailKit.Security;

using MimeKit;

using PaperRoute.Config;
using PaperRoute.Delivery;
using PaperRoute.Smtp;

namespace PaperRoute.Core.Tests.Fakes;

/// <summary>A running <see cref="SmtpHost"/> on a free loopback port with fake readers.</summary>
internal sealed class HostFixture : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> _warnings = new();

    private HostFixture(SmtpHost host, AppConfig config, FakeDevice device)
    {
        Host = host;
        Config = config;
        Device = device;
        host.SecurityWarning += (_, e) => _warnings.Enqueue(e.Message);
        host.Delivered += (_, e) => LastOutcome = e.Outcome;
    }

    public SmtpHost Host { get; }

    public AppConfig Config { get; }

    public FakeDevice Device { get; }

    public int Port => AppConfig.ParseListen(Config.Listen).Port;

    public DeliveryOutcome? LastOutcome { get; private set; }

    public IReadOnlyCollection<string> Warnings => _warnings;

    public static async Task<HostFixture> StartAsync(int readers = 1, int maxMB = 200, IBookConverter? converter = null, SmtpTimeouts? timeouts = null, MemoryLog? log = null, Exception? deviceFailure = null)
    {
        var device = new FakeDevice("Kindle") { ThrowOnWrite = deviceFailure };
        IReaderDevice[] devices = readers == 0 ? [] : [device, .. Enumerable.Range(1, readers - 1).Select(_ => new FakeDevice("Kindle 2"))];
        var config = TestConfig.Instance with { Listen = $"127.0.0.1:{FreePort()}", MaxMessageMB = maxMB };
#pragma warning disable CA2000 // Ownership passes to the returned HostFixture, which disposes it.
        var host = new SmtpHost(config, new FakeLocator(devices), log ?? new MemoryLog(), _ => converter, timeouts);
#pragma warning restore CA2000
        try
        {
            var result = await host.StartAsync();
            Assert.True(result.Ok, result.Error);
            return new HostFixture(host, config, device);
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    public static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    /// <summary>Sends the way CWA does: plain SMTP, AUTH when a password is set, text body + one attachment.</summary>
    public Task SendLikeCwaAsync(string? attachment = "a.azw3", int bytes = 1024, string? login = "", string? password = null, string to = "kindle@paperroute.local", int? port = null) =>
        SendAsync(port ?? Port, attachment, bytes, login is { Length: 0 } ? Config.AuthLogin : login, password ?? Config.AuthPassword, to);

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    private static async Task SendAsync(int port, string? attachment, int bytes, string? login, string password, string to)
    {
        var builder = new BodyBuilder { TextBody = "This e-mail has been sent via Calibre-Web." };
        if (attachment is not null)
        {
            using var content = new MemoryStream(BookBytes.For(attachment, bytes));
            await builder.Attachments.AddAsync(attachment, content, TestContext.Current.CancellationToken);
        }

        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("cwa@localhost"));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = "Send to eReader";
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync("127.0.0.1", port, SecureSocketOptions.None, TestContext.Current.CancellationToken);
        if (login is not null)
        {
            await client.AuthenticateAsync(login, password, TestContext.Current.CancellationToken);
        }

        await client.SendAsync(message, TestContext.Current.CancellationToken);
        await client.DisconnectAsync(quit: true, TestContext.Current.CancellationToken);
    }
}
