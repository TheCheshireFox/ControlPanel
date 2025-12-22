using System.Buffers;

namespace ControlPanel.Bridge.Framer;

internal static class Crc16Ccitt
{
    private const ushort Polynomial = 0x1021;

    public static ushort Compute(SequenceReader<byte> reader, int size, ushort initial = 0xFFFF)
    {
        var crc = initial;

        while (size-- > 0 && reader.TryRead(out var b))
        {
            crc = ComputeByte(crc, b);
        }

        return crc;
    }

    public static ushort Compute(ReadOnlySpan<byte> data, ushort initial = 0xFFFF)
    {
        var crc = initial;
        
        foreach (var b in data)
        {
            crc = ComputeByte(crc, b);
        }
        
        return crc;
    }

    private static ushort ComputeByte(ushort crc, byte b)
    {
        crc ^= (ushort)(b << 8);
        for (var i = 0; i < 8; i++)
        {
            crc = (crc & 0x8000) != 0
                ? (ushort)((crc << 1) ^ Polynomial)
                : (ushort)(crc << 1);
        }
        
        return crc;
    }
}