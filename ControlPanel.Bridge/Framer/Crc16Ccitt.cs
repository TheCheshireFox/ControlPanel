namespace ControlPanel.Bridge.Framer;

internal static class Crc16Ccitt
{
    private const ushort Polynomial = 0x1021;

    public static ushort Compute(IEnumerable<MemoryBlock> data, ushort initial = 0xFFFF)
    {
        var crc = initial;

        foreach (var dataBlock in data)
        {
            foreach (var b in dataBlock.Span[..dataBlock.Count])
            {
                crc ^= (ushort)(b << 8);
                for (var i = 0; i < 8; i++)
                {
                    crc = (crc & 0x8000) != 0
                        ? (ushort)((crc << 1) ^ Polynomial)
                        : (ushort)(crc << 1);
                }
            }
        }

        return crc;
    }

    public static ushort Compute(byte[] data, ushort initial = 0xFFFF)
    {
        var crc = initial;
        
        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ Polynomial)
                    : (ushort)(crc << 1);
            }
        }
        
        return crc;
    }
}