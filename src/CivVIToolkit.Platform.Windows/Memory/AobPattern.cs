namespace CivVIToolkit.Platform.Windows.Memory;

public sealed class AobPattern
{
    public byte[] Bytes { get; }
    public bool[] Wildcards { get; }
    public int Length => Bytes.Length;

    private AobPattern(byte[] bytes, bool[] wildcards)
    {
        Bytes = bytes;
        Wildcards = wildcards;
    }

    public static AobPattern Parse(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var tokens = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var bytes = new byte[tokens.Length];
        var wildcards = new bool[tokens.Length];

        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (token is "?" or "??")
            {
                wildcards[index] = true;
                continue;
            }

            if (!byte.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out bytes[index]))
            {
                throw new FormatException($"Invalid AoB token '{token}' at index {index}.");
            }
        }

        if (bytes.Length == 0)
        {
            throw new FormatException("AoB pattern cannot be empty.");
        }

        return new AobPattern(bytes, wildcards);
    }

    internal bool IsMatch(ReadOnlySpan<byte> data, int offset)
    {
        for (var index = 0; index < Bytes.Length; index++)
        {
            if (!Wildcards[index] && data[offset + index] != Bytes[index])
            {
                return false;
            }
        }

        return true;
    }
}
