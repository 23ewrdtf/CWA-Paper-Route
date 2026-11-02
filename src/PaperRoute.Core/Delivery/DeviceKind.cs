namespace PaperRoute.Delivery;

/// <summary>How an e-reader is connected to the PC.</summary>
public enum DeviceKind
{
    /// <summary>USB mass storage: the reader appears as a drive letter.</summary>
    MassStorage,

    /// <summary>Media Transfer Protocol: the reader appears as a portable device (experimental).</summary>
    Mtp,
}
