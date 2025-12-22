using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace ControlPanel.Bridge.Framer;

public enum FrameType : byte
{
    Undefined = 0xFF,
    Data = 0,
    ACK = 1
}

public class Frame(ushort sequence = 0, FrameType type = FrameType.Undefined, byte[]? data = null)
{
    public readonly ushort Sequence = sequence;
    public readonly FrameType Type = type;
    public readonly byte[] Data = data ?? [];
}

// format magic + seq(u16) + type(u8) + len(u16) + data + crc16
public sealed class Framer
{
    private readonly Memory<byte> _magic;
    private readonly Memory<byte> _magicFrameBuffer;
    private readonly ILogger _logger;

    private readonly (FrameField Type, int Size)[] _frameFieldSizes;

    public Framer(byte[] magic, ILogger logger)
    {
        _magic = magic;
        _magicFrameBuffer = new Memory<byte>(new byte[magic.Length]);
        _logger = logger;

        _frameFieldSizes =
        [
            (FrameField.Magic, magic.Length),
            (FrameField.Length, sizeof(ushort)),
            (FrameField.Sequence, sizeof(ushort)),
            (FrameField.Type, sizeof(FrameType)),
            (FrameField.Data, 0), // dynamic
            (FrameField.Crc16, sizeof(ushort)),
        ];
    }

    public int ToBytes(Frame frame, Memory<byte> dst)
    {
        _logger.LogDebug("Frame to bytes, seq={Sequence}, type={Type}, size={Size}", frame.Sequence, frame.Type, frame.Data.Length);

        var mem = dst;
        Span<byte> buffer = stackalloc byte[2];
        
        Write(_magic.Span);
        WriteUInt16BigEndian(buffer, (ushort)frame.Data.Length);
        WriteUInt16BigEndian(buffer, frame.Sequence);
        Write([(byte)frame.Type]);
        Write(frame.Data);
        WriteUInt16BigEndian(buffer, Crc16Ccitt.Compute(dst[..^mem.Length].Span));
        
        return dst.Length - mem.Length;

        void Write(Span<byte> src)
        {
            src.CopyTo(mem.Span);
            mem = mem[src.Length..];
        }

        void WriteUInt16BigEndian(Span<byte> buffer, ushort value)
        {
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            Write(buffer);
        }
    }

    public int GetFrameSize(int dataSize) => GetFrameSizeInternal(dataSize);
    
    private int GetFrameSizeInternal(int dataSize, params FrameField[] exclude)
    {
        return _frameFieldSizes.Where(x => !exclude.Contains(x.Type)).Sum(x => x.Size) + dataSize;
    }

    public bool TryParseFrame(ref SequenceReader<byte> reader, [NotNullWhen(true)] out Frame? frame)
    {
        frame = null;

        SequenceReader<byte> frameReader;
        while (true)
        {
            if (!reader.TryAdvanceTo(_magic.Span[0], false))
            {
                reader.AdvanceToEnd();
                return false;
            }
            
            if (!reader.TryCopyTo(_magicFrameBuffer.Span))
                return false;

            if (_magicFrameBuffer.Span.SequenceEqual(_magic.Span))
            {
                frameReader = reader;
                frameReader.Advance(_magic.Length);
                break;
            }

            reader.Advance(1);
        }

        if (!frameReader.TryReadBigEndian(out var len))
            return false;
        
        if (len + sizeof(ushort) + sizeof(FrameType) + sizeof(ushort) > frameReader.Remaining)
            return false;
        
        if (!frameReader.TryReadBigEndian(out var seq))
            return false;
        
        if (!frameReader.TryRead(out var type))
            return false;
        
        if (!frameReader.TryReadExactBytes(len, out var frameData))
            return false;
        
        if (!frameReader.TryReadBigEndian(out var crc16))
            return false;

        var frameCrc16 = Crc16Ccitt.Compute(reader, GetFrameSizeInternal(len, exclude: FrameField.Crc16));

        reader = frameReader;
        
        if (frameCrc16 != crc16)
        {
            _logger.LogError("bad crc {Expected} != {Calculated}", frameCrc16, crc16);
            return false;
        }

        frame = new Frame(seq, (FrameType)type, frameData);
        return true;
    }

    private enum FrameField
    {
        Magic,
        Length,
        Sequence,
        Type,
        Data,
        Crc16
    }
}