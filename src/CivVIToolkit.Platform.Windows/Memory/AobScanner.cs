namespace CivVIToolkit.Platform.Windows.Memory;

public sealed class AobScanner(ProcessMemoryAccessor memory)
{
    private const int DefaultChunkSize = 1024 * 1024;
    private const int ReadFailureAdvance = 4096;

    public nint? FindFirst(nint startAddress, int length, AobPattern pattern)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentNullException.ThrowIfNull(pattern);

        var end = startAddress + length;
        var cursor = startAddress;
        var overlap = Math.Max(0, pattern.Length - 1);

        while (cursor < end)
        {
            var remaining = checked((long)(end - cursor));
            var requested = (int)Math.Min(DefaultChunkSize, remaining);
            if (!memory.TryRead(cursor, requested, out var buffer))
            {
                cursor += Math.Min(ReadFailureAdvance, requested);
                continue;
            }

            var lastOffset = buffer.Length - pattern.Length;
            for (var offset = 0; offset <= lastOffset; offset++)
            {
                if (pattern.IsMatch(buffer, offset))
                {
                    return cursor + offset;
                }
            }

            var advance = Math.Max(1, buffer.Length - overlap);
            cursor += advance;
        }

        return null;
    }
}
