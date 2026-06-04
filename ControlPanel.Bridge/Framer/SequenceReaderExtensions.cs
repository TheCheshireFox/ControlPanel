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
}