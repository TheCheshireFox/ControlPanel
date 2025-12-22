using System.Buffers;

namespace ControlPanel.Bridge.Framer;

public static class SequenceReaderExtensions
{
    public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out ushort value)
    {
        if (!reader.TryReadBigEndian(out short v))
        {
            value = 0;
            return false;
        }
        
        value = (ushort)v;
        return true;
    }

    public static bool TryReadExactBytes(ref this SequenceReader<byte> reader, int count, out byte[] bytes)
    {
        if (reader.Remaining < count)
        {
            bytes = null!;
            return false;
        }
        
        bytes = new byte[count];
        if (!reader.TryCopyTo(bytes))
            return false;
        
        reader.Advance(count);
        return true;
    }
}