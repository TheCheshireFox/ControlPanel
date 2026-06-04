using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using ControlPanel.Bridge.Extensions;

namespace ControlPanel.Bridge.Framer;

// format magic + len(u16) + data
public sealed class Framer(byte[] magic, ILogger logger)
{
    private readonly Memory<byte> _magic = magic;

    private readonly (FrameField Type, int Size)[] _frameFieldSizes =
    [
        (FrameField.Magic, magic.Length),
        (FrameField.Length, sizeof(ushort))
    ];

    public byte[] ToBytes(ReadOnlyMemory<byte> data)
    {
        logger.LogDebug("Frame to bytes, size={Size}", data.Length);

        using var stream = new MemoryStream(new byte[GetFrameSize(data.Length)]);
        stream.Write(_magic.Span);
        stream.WriteUInt16BigEndian((ushort)data.Length);
        stream.Write(data.Span);
        
        return stream.ToArray();
    }

    public int GetFrameSize(int dataSize) => _frameFieldSizes.Sum(x => x.Size) + dataSize;

    public bool TryParseFrame(ref SequenceReader<byte> reader, [NotNullWhen(true)] out ReadOnlyMemory<byte>? frame)
    {
        frame = null;

        Span<byte> magicBuffer = stackalloc byte[_magic.Length];
        
        SequenceReader<byte> frameReader;
        while (true)
        {
            if (!reader.TryAdvanceTo(_magic.Span[0], false))
            {
                reader.AdvanceToEnd();
                return false;
            }
            
            if (!reader.TryCopyTo(magicBuffer))
                return false;

            if (magicBuffer.SequenceEqual(_magic.Span))
            {
                frameReader = reader;
                frameReader.Advance(_magic.Length);
                break;
            }

            reader.Advance(1);
        }

        if (!frameReader.TryReadBigEndian(out var len))
            return false;
        
        if (len > frameReader.Remaining)
            return false;

        if (!frameReader.TryReadExact(len, out var frameSequence))
            return false;

        frame = frameSequence.IsSingleSegment
            ? frameSequence.First
            : frameSequence.ToArray();

        reader = frameReader;
        return true;
    }

    private enum FrameField
    {
        Magic,
        Length
    }
}