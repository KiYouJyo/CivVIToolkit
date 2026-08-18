using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CivVIToolkit.Platform.Windows.Memory;

/// <summary>
/// Minimal Windows x64 remote-call bridge used only for exact, fingerprint-gated
/// Civilization VI GameCore routines. It never injects a persistent DLL: a tiny
/// temporary call stub and optional Int32 argument are allocated, executed, read
/// back and released for each call.
/// </summary>
public sealed class RemoteProcessInvoker : IDisposable
{
    private const uint ProcessCreateThread = 0x0002;
    private const uint ProcessVmOperation = 0x0008;
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessVmWrite = 0x0020;
    private const uint ProcessQueryInformation = 0x0400;
    private const uint MemCommit = 0x1000;
    private const uint MemReserve = 0x2000;
    private const uint MemRelease = 0x8000;
    private const uint PageExecuteReadWrite = 0x40;
    private const uint WaitObject0 = 0x00000000;
    private const uint WaitTimeout = 0x00000102;
    private const uint Infinite = 0xFFFFFFFF;
    private const int AllocationSize = 0x1000;
    private const int ResultOffset = 0x100;
    private const int DataOffset = 0x200;

    private nint _handle;

    public RemoteProcessInvoker(int processId)
    {
        var access = ProcessCreateThread | ProcessVmOperation | ProcessVmRead | ProcessVmWrite | ProcessQueryInformation;
        _handle = OpenProcess(access, false, processId);
        if (_handle == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to open process {processId} for a verified GameCore call.");
        }
    }

    public ulong Invoke(
        nint function,
        ulong rcx = 0,
        ulong rdx = 0,
        ulong r8 = 0,
        ulong r9 = 0,
        int? int32PointerValue = null,
        int pointerArgumentIndex = 0,
        TimeSpan? timeout = null)
    {
        ObjectDisposedException.ThrowIf(_handle == 0, this);
        if (function == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(function));
        }

        if (int32PointerValue.HasValue && pointerArgumentIndex is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(pointerArgumentIndex), "Pointer argument index must be 1 (RCX) through 4 (R9).");
        }

        var remote = VirtualAllocEx(
            _handle,
            0,
            AllocationSize,
            MemCommit | MemReserve,
            PageExecuteReadWrite);
        if (remote == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualAllocEx failed for the temporary GameCore call bridge.");
        }

        try
        {
            var resultAddress = remote + ResultOffset;
            var dataAddress = remote + DataOffset;

            if (int32PointerValue.HasValue)
            {
                WriteRemote(dataAddress, BitConverter.GetBytes(int32PointerValue.Value));
                var pointer = unchecked((ulong)(nuint)dataAddress);
                switch (pointerArgumentIndex)
                {
                    case 1: rcx = pointer; break;
                    case 2: rdx = pointer; break;
                    case 3: r8 = pointer; break;
                    case 4: r9 = pointer; break;
                }
            }

            var stub = RemoteCallStubBuilder.Build(
                unchecked((ulong)(nuint)function),
                rcx,
                rdx,
                r8,
                r9,
                unchecked((ulong)(nuint)resultAddress));
            WriteRemote(remote, stub);
            WriteRemote(resultAddress, new byte[sizeof(ulong)]);

            var thread = CreateRemoteThread(_handle, 0, 0, remote, 0, 0, out _);
            if (thread == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateRemoteThread failed for the temporary GameCore call bridge.");
            }

            try
            {
                var milliseconds = timeout.HasValue
                    ? checked((uint)Math.Clamp(timeout.Value.TotalMilliseconds, 1, uint.MaxValue - 1d))
                    : 5_000u;
                var wait = WaitForSingleObject(thread, milliseconds);
                if (wait == WaitTimeout)
                {
                    throw new TimeoutException("The verified GameCore call did not complete within the timeout.");
                }

                if (wait != WaitObject0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"WaitForSingleObject returned 0x{wait:X8}.");
                }
            }
            finally
            {
                CloseHandle(thread);
            }

            var resultBytes = ReadRemote(resultAddress, sizeof(ulong));
            return BitConverter.ToUInt64(resultBytes, 0);
        }
        finally
        {
            VirtualFreeEx(_handle, remote, 0, MemRelease);
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

    private void WriteRemote(nint address, byte[] bytes)
    {
        if (!WriteProcessMemory(_handle, address, bytes, (nuint)bytes.Length, out var written)
            || written != (nuint)bytes.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"WriteProcessMemory failed at 0x{address:X} while preparing a GameCore call.");
        }
    }

    private byte[] ReadRemote(nint address, int length)
    {
        var bytes = new byte[length];
        if (!ReadProcessMemory(_handle, address, bytes, (nuint)length, out var read)
            || read != (nuint)length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"ReadProcessMemory failed at 0x{address:X} while completing a GameCore call.");
        }

        return bytes;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint VirtualAllocEx(nint process, nint address, nuint size, uint allocationType, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFreeEx(nint process, nint address, nuint size, uint freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateRemoteThread(
        nint process,
        nint threadAttributes,
        nuint stackSize,
        nint startAddress,
        nint parameter,
        uint creationFlags,
        out uint threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(nint handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(nint process, nint baseAddress, [Out] byte[] buffer, nuint size, out nuint bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(nint process, nint baseAddress, byte[] buffer, nuint size, out nuint bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}

internal static class RemoteCallStubBuilder
{
    public static byte[] Build(ulong function, ulong rcx, ulong rdx, ulong r8, ulong r9, ulong resultAddress)
    {
        using var stream = new MemoryStream(96);
        using var writer = new BinaryWriter(stream);

        // Windows x64 ABI: reserve 32-byte shadow space and preserve 16-byte alignment.
        writer.Write(new byte[] { 0x48, 0x83, 0xEC, 0x28 }); // sub rsp, 28h
        WriteMovImm64(writer, new byte[] { 0x48, 0xB9 }, rcx); // rcx
        WriteMovImm64(writer, new byte[] { 0x48, 0xBA }, rdx); // rdx
        WriteMovImm64(writer, new byte[] { 0x49, 0xB8 }, r8);  // r8
        WriteMovImm64(writer, new byte[] { 0x49, 0xB9 }, r9);  // r9
        WriteMovImm64(writer, new byte[] { 0x48, 0xB8 }, function); // rax=function
        writer.Write(new byte[] { 0xFF, 0xD0 }); // call rax
        WriteMovImm64(writer, new byte[] { 0x49, 0xBA }, resultAddress); // r10=result
        writer.Write(new byte[] { 0x49, 0x89, 0x02 }); // mov [r10], rax
        writer.Write(new byte[] { 0x33, 0xC0 }); // xor eax,eax (thread exit code)
        writer.Write(new byte[] { 0x48, 0x83, 0xC4, 0x28 }); // add rsp,28h
        writer.Write((byte)0xC3); // ret
        return stream.ToArray();
    }

    private static void WriteMovImm64(BinaryWriter writer, byte[] opcode, ulong value)
    {
        writer.Write(opcode);
        writer.Write(value);
    }
}
