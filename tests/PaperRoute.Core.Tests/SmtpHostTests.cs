using System.Net;
using System.Net.Sockets;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

using PaperRoute.Config;
using PaperRoute.Core.Tests.Fakes;
using PaperRoute.Delivery;
using PaperRoute.Smtp;

namespace PaperRoute.Core.Tests;

public sealed class SmtpHostTests
{
    [Fact]
    public async Task Sends_like_cwa_and_gets_delivered()
    {
        await using var host = await HostFixture.StartAsync();

        await host.SendLikeCwaAsync("a.azw3");

        Assert.Equal(["a.azw3"], host.Device.Written);
        Assert.Equal(OutcomeKind.Delivered, host.LastOutcome?.Kind);
    }

    [Fact]
    public async Task Epub_is_converted_when_a_converter_is_configured()
    {
        await using var host = await HostFixture.StartAsync(converter: new FakeConverter());

        await host.SendLikeCwaAsync("Dune.epub");

        Assert.Equal(["Dune.azw3"], host.Device.Written);
    }

    [Fact]
    public async Task Slow_conversion_longer_than_the_command_timeout_still_delivers()
    {
        // The command timeout limits waiting for the client's next command, not the whole session:
        // a conversion longer than it must still end in "250 Delivered".
        await using var host = await HostFixture.StartAsync(
            converter: new FakeConverter { Delay = TimeSpan.FromSeconds(4) },
            timeouts: new SmtpTimeouts(Command: TimeSpan.FromSeconds(2), Session: TimeSpan.FromMinutes(1)));

        await host.SendLikeCwaAsync("Slow.epub");

        Assert.Equal(["Slow.azw3"], host.Device.Written);
        Assert.Equal(OutcomeKind.Delivered, host.LastOutcome?.Kind);
    }

    [Fact]
    public async Task Unexpected_device_error_gives_readable_451_and_is_logged()
    {
        var log = new MemoryLog();
        await using var host = await HostFixture.StartAsync(readers: 1, log: log, deviceFailure: new InvalidOperationException("WPD went away"));

        var error = await Assert.ThrowsAsync<SmtpCommandException>(() => host.SendLikeCwaAsync("a.azw3"));

        Assert.Equal(451, (int)error.StatusCode);
        Assert.Equal("Paper Route hit an internal error (InvalidOperationException) - nothing was copied, send again", error.Message);
        Assert.Contains(log.Lines, l => l.Contains("InvalidOperationException", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rejection_text_reaches_client()
    {
        await using var host = await HostFixture.StartAsync(readers: 0);

        var error = await Assert.ThrowsAsync<SmtpCommandException>(() => host.SendLikeCwaAsync("a.azw3"));

        Assert.Equal(550, (int)error.StatusCode);
        Assert.Equal("Kindle not connected - plug it in and send again", error.Message);
    }

    [Fact]
    public async Task Write_failure_is_451()
    {
        await using var host = await HostFixture.StartAsync(readers: 0);
        var error = await Assert.ThrowsAsync<SmtpCommandException>(() => host.SendLikeCwaAsync("a.epub"));

        Assert.Equal(550, (int)error.StatusCode);
        Assert.StartsWith("Install Calibre", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_email_without_attachment_is_accepted()
    {
        await using var host = await HostFixture.StartAsync(readers: 0);

        await host.SendLikeCwaAsync(attachment: null);

        Assert.Equal(OutcomeKind.TestMessage, host.LastOutcome?.Kind);
    }

    [Fact]
    public async Task Oversize_message_gets_552_with_our_text()
    {
        await using var host = await HostFixture.StartAsync(maxMB: 1);

        var error = await Assert.ThrowsAsync<SmtpCommandException>(() => host.SendLikeCwaAsync("big.azw3", bytes: 900_000));

        Assert.Equal(552, (int)error.StatusCode);
        Assert.Equal("Book is larger than the 1 MB limit set in Paper Route", error.Message);
        Assert.Empty(host.Device.Written);
    }

    [Fact]
    public async Task Server_advertises_hard_size_limit_above_configured_limit()
    {
        await using var host = await HostFixture.StartAsync(maxMB: 10);
        await using var smtp = await RawSmtp.ConnectAsync(host.Port);

        var lines = await smtp.SendMultilineAsync("EHLO test");

        var size = Assert.Single(lines, l => l.StartsWith("250-SIZE ", StringComparison.Ordinal) || l.StartsWith("250 SIZE ", StringComparison.Ordinal));
        Assert.Equal(SmtpHost.HardLimitBytes(10), long.Parse(size[9..], System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(SmtpHost.HardLimitBytes(10) > 10L * 1024 * 1024);
    }

    [Fact]
    public async Task Huge_message_is_refused_without_delivery()
    {
        await using var host = await HostFixture.StartAsync(maxMB: 1);

        var error = await Assert.ThrowsAsync<SmtpCommandException>(() => host.SendLikeCwaAsync("huge.azw3", bytes: 5_000_000));

        Assert.Equal(552, (int)error.StatusCode);
        Assert.Empty(host.Device.Written);
    }

    [Fact]
    public async Task Oversize_refusal_is_logged()
    {
        var log = new MemoryLog();
        await using var host = await HostFixture.StartAsync(maxMB: 1, log: log);

        await Assert.ThrowsAsync<SmtpCommandException>(() => host.SendLikeCwaAsync("big.azw3", bytes: 900_000));

        Assert.Contains(log.Lines, l => l.Contains("larger than", StringComparison.Ordinal));
    }

    [Fact]
    public async Task No_auth_is_530()
    {
        await using var host = await HostFixture.StartAsync();

        var error = await Assert.ThrowsAsync<ServiceNotAuthenticatedException>(() => host.SendLikeCwaAsync("a.azw3", login: null));

        Assert.Equal("Authentication required - enter Paper Route's login and password in CWA's e-mail settings", error.Message);
        Assert.Empty(host.Device.Written);
        Assert.Contains(host.Warnings, w => w.StartsWith("Refused", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Wrong_password_is_535_and_warns()
    {
        await using var host = await HostFixture.StartAsync();

        var error = await Assert.ThrowsAsync<AuthenticationException>(() => host.SendLikeCwaAsync("a.azw3", password: "nope"));

        Assert.Contains("Wrong login or password - copy them again from Paper Route's Settings window", error.Message, StringComparison.Ordinal);
        Assert.Contains(host.Warnings, w => w.StartsWith("Refused", StringComparison.Ordinal));
        Assert.Empty(host.Device.Written);
    }

    [Fact]
    public async Task Wrong_login_is_refused()
    {
        await using var host = await HostFixture.StartAsync();

        await Assert.ThrowsAsync<AuthenticationException>(() => host.SendLikeCwaAsync("a.azw3", login: "admin"));
    }

    [Fact]
    public async Task Cwa_test_email_to_the_admins_own_address_is_accepted()
    {
        // CWA's "Save and send test e-mail" goes to the admin's profile address, not the eReader address.
        await using var host = await HostFixture.StartAsync(readers: 0);

        await host.SendLikeCwaAsync(attachment: null, to: "admin@example.org");

        Assert.Equal(OutcomeKind.TestMessage, host.LastOutcome?.Kind);
    }

    [Fact]
    public async Task Other_recipient_is_550_no_relay()
    {
        await using var host = await HostFixture.StartAsync();

        var error = await Assert.ThrowsAsync<SmtpCommandException>(() => host.SendLikeCwaAsync("a.azw3", to: "victim@example.com"));

        Assert.Equal(550, (int)error.StatusCode);
        Assert.Equal("Unknown recipient - Paper Route only accepts mail for kindle@paperroute.local", error.Message);
        Assert.Empty(host.Device.Written);
    }

    [Fact]
    public async Task Recipient_match_ignores_case()
    {
        await using var host = await HostFixture.StartAsync();

        await host.SendLikeCwaAsync("a.azw3", to: "Kindle@PaperRoute.Local");

        Assert.Single(host.Device.Written);
    }

    [Fact]
    public async Task Third_failed_login_closes_the_connection()
    {
        await using var host = await HostFixture.StartAsync();
        await using var smtp = await RawSmtp.ConnectAsync(host.Port);
        await smtp.ExpectAsync("EHLO test", "250");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await smtp.ExpectAsync(RawSmtp.AuthPlain("cwa", "bad"), "535");
        }

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var reply = await smtp.SendAsync(RawSmtp.AuthPlain("cwa", host.Config.AuthPassword));
            Assert.Fail($"Connection should be closed, got: {reply}");
        });
        Assert.Empty(host.Device.Written);
    }

    [Fact]
    public async Task Wrong_password_reply_starts_with_our_text()
    {
        await using var host = await HostFixture.StartAsync();
        await using var smtp = await RawSmtp.ConnectAsync(host.Port);
        await smtp.ExpectAsync("EHLO test", "250");

        var reply = await smtp.SendAsync(RawSmtp.AuthPlain("cwa", "bad"));

        Assert.StartsWith("535 Wrong login or password - copy them again from Paper Route's Settings window", reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Port_in_use_returns_error_not_exception()
    {
        using var blocker = new TcpListener(IPAddress.Loopback, 0);
        blocker.Start();
        var port = ((IPEndPoint)blocker.LocalEndpoint).Port;
        await using var host = new SmtpHost(TestConfig.Instance with { Listen = $"127.0.0.1:{port}" }, new FakeLocator(), new MemoryLog());

        var result = await host.StartAsync();

        Assert.False(result.Ok);
        Assert.Equal($"Port {port} is already in use by another program", result.Error);
    }

    [Fact]
    public async Task Restart_on_busy_port_keeps_old_listener()
    {
        await using var host = await HostFixture.StartAsync();
        using var blocker = new TcpListener(IPAddress.Loopback, 0);
        blocker.Start();
        var busy = ((IPEndPoint)blocker.LocalEndpoint).Port;

        var result = await host.Host.RestartAsync(host.Config with { Listen = $"127.0.0.1:{busy}" });

        Assert.False(result.Ok);
        await host.SendLikeCwaAsync("a.azw3");
        Assert.Single(host.Device.Written);
    }

    [Fact]
    public async Task Restart_on_new_port_moves_listener()
    {
        await using var host = await HostFixture.StartAsync();
        var newPort = HostFixture.FreePort();

        var result = await host.Host.RestartAsync(host.Config with { Listen = $"127.0.0.1:{newPort}" });

        Assert.True(result.Ok, result.Error);
        await host.SendLikeCwaAsync("a.azw3", port: newPort);
        Assert.Single(host.Device.Written);
        await Assert.ThrowsAnyAsync<Exception>(() => host.SendLikeCwaAsync("b.azw3"));
    }

    [Fact]
    public async Task Restart_on_same_port_applies_new_password()
    {
        await using var host = await HostFixture.StartAsync();

        var result = await host.Host.RestartAsync(host.Config with { AuthPassword = "NewPassword123" });

        Assert.True(result.Ok, result.Error);
        await host.SendLikeCwaAsync("a.azw3", password: "NewPassword123");
        await Assert.ThrowsAsync<AuthenticationException>(() => host.SendLikeCwaAsync("b.azw3"));
    }

    [Fact]
    public async Task Fifth_concurrent_session_is_refused()
    {
        await using var host = await HostFixture.StartAsync();
        var clients = new List<SmtpClient>();
        try
        {
            for (var i = 0; i < SmtpHost.MaxSessions; i++)
            {
                var c = new SmtpClient();
                clients.Add(c);
                await c.ConnectAsync("127.0.0.1", host.Port, SecureSocketOptions.None, TestContext.Current.CancellationToken);
            }

            await Assert.ThrowsAnyAsync<Exception>(() => host.SendLikeCwaAsync("a.azw3"));
        }
        finally
        {
            foreach (var c in clients)
            {
                c.Dispose();
            }
        }

        Assert.Empty(host.Device.Written);
    }

    [Fact]
    public async Task Disallowed_client_is_refused()
    {
        var config = TestConfig.Instance with { Listen = $"127.0.0.1:{HostFixture.FreePort()}", AllowedClients = ["10.0.0.0/8"] };
        var device = new FakeDevice("Kindle");
        await using var host = new SmtpHost(config, new FakeLocator(device), new MemoryLog());
        var warnings = new List<string>();
        host.SecurityWarning += (_, e) => warnings.Add(e.Message);
        Assert.True((await host.StartAsync()).Ok);
        using var client = new SmtpClient();
        await client.ConnectAsync("127.0.0.1", AppConfig.ParseListen(config.Listen).Port, SecureSocketOptions.None, TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<Exception>(() => client.AuthenticateAsync(config.AuthLogin, config.AuthPassword, TestContext.Current.CancellationToken));

        Assert.Empty(device.Written);
        Assert.Contains(warnings, w => w.Contains("127.0.0.1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Second_recipient_is_refused()
    {
        await using var host = await HostFixture.StartAsync();
        await using var smtp = await RawSmtp.ConnectAsync(host.Port);
        await smtp.ExpectAsync("EHLO test", "250");
        await smtp.ExpectAsync(RawSmtp.AuthPlain(host.Config.AuthLogin, host.Config.AuthPassword), "235");
        await smtp.ExpectAsync("MAIL FROM:<cwa@localhost>", "250");
        await smtp.ExpectAsync("RCPT TO:<kindle@paperroute.local>", "250");

        var reply = await smtp.SendAsync("RCPT TO:<kindle@paperroute.local>");

        Assert.Equal("550 Only one recipient per message", reply);
        Assert.Empty(host.Device.Written);
    }
}
