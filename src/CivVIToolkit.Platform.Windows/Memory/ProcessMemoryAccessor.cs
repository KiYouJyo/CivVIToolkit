using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CivVIToolkit.Platform.Windows.Memory;

public sealed class ProcessMemoryAccessor : IDisposable
{
    private const uint ProcessVmOperation = 0x0008;
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessVmWrite = 0x0020;
    private const uint ProcessQueryInformation = 0x0400;
    private const uint PageExecuteReadWrite = 0x40;

    private nint _handle;

    public int ProcessId { get; }
    public bool CanWrite { get; }

    public ProcessMemoryAccessor(int processId, bool allowWrite = true)
    {
        ProcessId = processId;
        CanWrite = allowWrite;
        var desiredAccess = ProcessVmRead | ProcessQueryInformation;
        if (allowWrite)
        {
            desiredAccess |= ProcessVmOperation | ProcessVmWrite;
        }

        _handle = OpenProcess(desiredAccess, false, processId);
        if (_handle == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to open process {processId}.");
        }
    }

    public byte[] Read(nint address, int length)
    {
        ObjectDisposedException.ThrowIf(_handle == 0, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        var buffer = new byte[length];
        if (!ReadProcessMemory(_handle, address, buffer, (nuint)buffer.Length, out var bytesRead))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"ReadProcessMemory failed at 0x{address:X}.");
        }

        if (bytesRead == (nuint)buffer.Length)
        {
            return buffer;
        }

        Array.Resize(ref buffer, checked((int)bytesRead));
        return buffer;
    }

    public bool TryRead(nint address, int length, out byte[] bytes)
    {
        try
        {
            bytes = Read(address, length);
            return bytes.Length > 0;
        }
        catch (Win32Exception)
        {
            bytes = [];
            return false;
        }
    }

    public void Write(nint address, ReadOnlySpan<byte> bytes)
    {
        ObjectDisposedException.ThrowIf(_handle == 0, this);
        if (!CanWrite)
        {
            throw new InvalidOperationException("This process memory accessor was opened in read-only mode.");
        }

        if (bytes.IsEmpty)
        {
            return;
        }

        var buffer = bytes.ToArray();
        if (!WriteProcessMemory(_handle, address, buffer, (nuint)buffer.Length, out var bytesWritten)
            || bytesWritten != (nuint)buffer.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"WriteProcessMemory failed at 0x{address:X}.");
        }
    }

    /// <summary>
    /// Writes a verified executable-code patch and restores the page protection
    /// immediately afterwards. Callers are responsible for validating original
    /// bytes and preserving them for rollback before using this method.
    /// </summary>
    public void WriteProtected(nint address, ReadOnlySpan<byte> bytes)
    {
        ObjectDisposedException.ThrowIf(_handle == 0, this);
        if (!CanWrite)
        {
            throw new InvalidOperationException("This process memory accessor was opened in read-only mode.");
        }

        if (bytes.IsEmpty)
        {
            return;
        }

        if (!VirtualProtectEx(_handle, address, (nuint)bytes.Length, PageExecuteReadWrite, out var oldProtection))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"VirtualProtectEx failed at 0x{address:X}.");
        }

        try
        {
            Write(address, bytes);
            if (!FlushInstructionCache(_handle, address, (nuint)bytes.Length))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"FlushInstructionCache failed at 0x{address:X}.");
            }
        }
        finally
        {
            VirtualProtectEx(_handle, address, (nuint)bytes.Length, oldProtection, out _);
        }
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0)
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(
        nint process,
        nint baseAddress,
        [Out] byte[] buffer,
        nuint size,
        out nuint bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(
        nint process,
        nint baseAddress,
        byte[] buffer,
        nuint size,
        out nuint bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualProtectEx(nint process, nint address, nuint size, uint newProtection, out uint oldProtection);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushInstructionCache(nint process, nint baseAddress, nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}
