using System.Globalization;
using System.Net;
using System.Net.Sockets;

using PaperRoute.CompatHost;
using PaperRoute.Config;
using PaperRoute.Smtp;

// Test-only host for tests/compat/run_compat.py: real SmtpHost, "reader" = a temp folder.
var outDir = Path.Combine(Path.GetTempPath(), "paperroute-compat-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(outDir);
int port;
using (var probe = new TcpListener(IPAddress.Loopback, 0))
{
    probe.Start();
    port = ((IPEndPoint)probe.LocalEndpoint).Port;
}

var config = new AppConfig(Listen: string.Create(CultureInfo.InvariantCulture, $"127.0.0.1:{port}"), AuthPassword: ConfigStore.NewPassword());
await using var host = new SmtpHost(config, new FolderLocator(outDir), new ConsoleLog());
var result = await host.StartAsync();
if (!result.Ok)
{
    await Console.Error.WriteLineAsync(result.Error);
    return 1;
}

Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"READY {port} {config.AuthPassword} {outDir}"));
await Console.Out.FlushAsync();
_ = await Console.In.ReadToEndAsync(); // run until the driver closes stdin
return 0;
