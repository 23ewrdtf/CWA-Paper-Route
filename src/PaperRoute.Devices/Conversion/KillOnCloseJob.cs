using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace PaperRoute.Devices.Conversion;

/// <summary>
/// A Windows job object that kills every process in it - including helpers they started - when disposed.
/// Used so a Calibre conversion never leaves processes behind (they would hold the temp folder and its
/// output pipes open).
/// </summary>
internal sealed partial class KillOnCloseJob : IDisposable
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint KillOnJobClose = 0x2000;

    private readonly SafeJobHandle _handle;

    private KillOnCloseJob(SafeJobHandle handle) => _handle = handle;

    /// <summary>Creates the job; <see langword="null"/> if Windows refuses (the caller then runs without it).</summary>
    public static KillOnCloseJob? TryCreate()
    {
#pragma warning disable CA2000 // Ownership passes to the returned KillOnCloseJob; released in finally otherwise.
        var handle = CreateJobObjectW(IntPtr.Zero, null);
#pragma warning restore CA2000
        var handedOver = false;
        try
        {
            if (handle.IsInvalid)
            {
                return null;
            }

            var info = new ExtendedLimitInformation { Basic = new BasicLimitInformation { LimitFlags = KillOnJobClose } };
            if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<ExtendedLimitInformation>()))
            {
                return null;
            }

            handedOver = true;
            return new KillOnCloseJob(handle);
        }
        finally
        {
            // Released on every path except the one that hands the handle to the job object.
            if (!handedOver)
            {
                handle.Dispose();
            }
        }
    }

    /// <summary>Puts a process into the job; helpers it starts afterwards join automatically.</summary>
    public bool TryAdd(Process process) => AssignProcessToJobObject(_handle, process.SafeHandle);

    /// <summary>Kills everything still running in the job.</summary>
    public void Dispose() => _handle.Dispose();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeJobHandle CreateJobObjectW(IntPtr attributes, string? name);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(SafeJobHandle job, int infoClass, ref ExtendedLimitInformation info, uint length);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(SafeJobHandle job, SafeProcessHandle process);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        public BasicLimitInformation Basic;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeJobHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }
}
