using System.Buffers.Binary;

namespace ControlPanel.Bridge.Extensions;

public static class StreamExtensions
{
    public static void WriteUInt16BigEndian(this Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }
}