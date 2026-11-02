namespace PaperRoute.Devices.Tests;

/// <summary>
/// Test classes that use System.Drawing (GDI+) run one at a time: GDI+ is not reliably thread-safe and
/// parallel classes intermittently got a null JPEG encoder. Paper Route itself only uses GDI+ inside the
/// delivery lock, one book at a time.
/// </summary>
#pragma warning disable CA1515 // xUnit discovers collection definitions as public types.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GdiSerial
#pragma warning restore CA1515
{
    public const string Name = "GDI+";

    private GdiSerial()
    {
    }
}
