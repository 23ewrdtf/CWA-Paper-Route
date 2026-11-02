using System.Collections.Concurrent;

using PaperRoute.Logging;

namespace PaperRoute.Core.Tests.Fakes;

internal sealed class MemoryLog : IActivityLog
{
    private readonly ConcurrentQueue<string> _lines = new();

    public IReadOnlyCollection<string> Lines => _lines;

    public void Write(string line) => _lines.Enqueue(line);
}
