using System.Buffers.Binary;
using System.Security.Cryptography;

namespace ControlPanel.Agent.Shared;

public static class AudioStreamIconHash
{
    public static int Calculate(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            return 0;

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(data, hash);
        return BinaryPrimitives.ReadInt32LittleEndian(hash);
    }
}