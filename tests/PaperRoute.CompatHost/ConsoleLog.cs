using PaperRoute.Logging;

namespace PaperRoute.CompatHost;

internal sealed class ConsoleLog : IActivityLog
{
    public void Write(string line) => Console.Error.WriteLine(line);
}
