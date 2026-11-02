using System.Net.Sockets;
using System.Text;

namespace PaperRoute.Core.Tests.Fakes;

/// <summary>Speaks SMTP line by line, for protocol cases a mail library would smooth over.</summary>
internal sealed class RawSmtp : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    private RawSmtp(TcpClient client)
    {
        _client = client;
        var stream = client.GetStream();
        _reader = new StreamReader(stream, Encoding.ASCII);
        _writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
    }

    public static async Task<RawSmtp> ConnectAsync(int port)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync("127.0.0.1", port, TestContext.Current.CancellationToken);
            var smtp = new RawSmtp(client);
            var greeting = await smtp.ReadReplyAsync();
            Assert.StartsWith("220", greeting, StringComparison.Ordinal);
            return smtp;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public static string AuthPlain(string login, string password) =>
        "AUTH PLAIN " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{login}\0{password}"));

    /// <summary>Sends a command and returns the last line of the reply.</summary>
    public async Task<string> SendAsync(string command)
    {
        await _writer.WriteLineAsync(command.AsMemory(), TestContext.Current.CancellationToken);
        return await ReadReplyAsync();
    }

    /// <summary>Sends a command and returns every line of the reply.</summary>
    public async Task<IReadOnlyList<string>> SendMultilineAsync(string command)
    {
        await _writer.WriteLineAsync(command.AsMemory(), TestContext.Current.CancellationToken);
        var lines = new List<string>();
        while (true)
        {
            var line = await _reader.ReadLineAsync(TestContext.Current.CancellationToken) ?? string.Empty;
            lines.Add(line);
            if (line.Length < 4 || line[3] != '-')
            {
                return lines;
            }
        }
    }

    public async Task ExpectAsync(string command, string codePrefix)
    {
        var reply = await SendAsync(command);
        Assert.StartsWith(codePrefix, reply, StringComparison.Ordinal);
    }

    public ValueTask DisposeAsync()
    {
        _reader.Dispose();
        _writer.Dispose();
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<string> ReadReplyAsync()
    {
        while (true)
        {
            var line = await _reader.ReadLineAsync(TestContext.Current.CancellationToken) ?? string.Empty;
            if (line.Length < 4 || line[3] != '-')
            {
                return line;
            }
        }
    }
}
