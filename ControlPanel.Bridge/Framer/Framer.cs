using System.Buffers.Binary;
using System.Text;

namespace ControlPanel.Bridge.Framer;

public static class SpanExtensions
{
    public static string FormatString(this Span<byte> span)
    {
        var sb = new StringBuilder("[");
        foreach (var b in span)
        {
            sb.Append(b.ToString("X2"));
            sb.Append(", ");
        }
        
        sb.Remove(sb.Length - 2, 2);
        sb.Append(']');

        return sb.ToString();
    }
}

public enum FrameType : byte
{
    Undefined = 0xFF,
    Data = 0,
    ACK = 1
}

public record Frame(ushort Sequence, FrameType Type, byte[] Data);

// format magic + seq(u16) + type(u8) + len(u16) + data + crc16
public sealed class Framer
{
    private readonly byte[] _magic;
    private readonly ILogger _logger;

    private class StateData
    {
        public required State State { get; set; }
        public required MemoryBlock MagicBlock { get; init; }
        public required MemoryBlock SequenceBlock { get; init; }
        public required MemoryBlock TypeBlock { get; init; }
        public required MemoryBlock LengthBlock { get; init; }
        public required MemoryBlock BodyBlock { get; init; }
        public required MemoryBlock Crc16Block { get; init; }

        public ushort Sequence;
        public FrameType Type;
        public ushort Length;
        public ushort Crc16;

        public void Reset()
        {
            State = State.Magic;
            MagicBlock.Clear();
            SequenceBlock.Clear();
            TypeBlock.Clear();
            LengthBlock.Clear();
            BodyBlock.Clear();
            Crc16Block.Clear();
            Sequence = 0;
            Type = FrameType.Undefined;
            Length = 0;
            Crc16 = 0;
        }
    }

    private readonly StateData _state;

    public Framer(byte[] magic, int maxFrameSize, ILogger logger)
    {
        _magic = magic;
        _logger = logger;

        _state = new StateData()
        {
            State = State.Magic,
            MagicBlock = new MemoryBlock(_magic.Length),
            SequenceBlock = new MemoryBlock(sizeof(ushort)),
            TypeBlock = new MemoryBlock(sizeof(FrameType)),
            LengthBlock = new MemoryBlock(sizeof(ushort)),
            BodyBlock = new MemoryBlock(maxFrameSize),
            Crc16Block = new MemoryBlock(2),
            Length = 0
        };
    }

    public byte[] ToBytes(Frame frame)
    {
        _logger.LogInformation("Frame to bytes, seq={Sequence}, type={Type}, size={Size}", frame.Sequence, frame.Type, frame.Data.Length);
        
        Span<byte> buffer = stackalloc byte[2];
        
        var ms = new MemoryStream();
        ms.Write(_magic);
        
        BinaryPrimitives.WriteUInt16BigEndian(buffer, frame.Sequence);
        ms.Write(buffer);
        //_logger.LogInformation("BE16 sequence {SequenceArray}", buffer.FormatString());

        ms.Write([(byte)frame.Type]);
        
        BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)frame.Data.Length);
        ms.Write(buffer);
        //_logger.LogInformation("BE16 size {SizeArray}", buffer.FormatString());
        
        ms.Write(frame.Data);
        
        BinaryPrimitives.WriteUInt16BigEndian(buffer, Crc16Ccitt.Compute(ms.ToArray()));
        ms.Write(buffer);
        //_logger.LogInformation("BE16 crc16 {Crc16Array}", buffer.FormatString());

        return ms.ToArray();
    }
    
    public IEnumerable<Frame> Append(ReadOnlyMemory<byte> data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            switch (_state.State)
            {
                case State.Magic:
                    ReadMagic(data, i);
                    break;
                case State.Seq:
                    ReadValue(data.Span[i], _state.SequenceBlock, ref _state.Sequence, State.Type);
                    break;
                case State.Type:
                    ReadValue(data.Span[i], _state.TypeBlock, ref _state.Type, State.Length);
                    break;
                case State.Length:
                    ReadValue(data.Span[i], _state.LengthBlock, ref _state.Length, State.Body, l => _state.Type == FrameType.ACK || l > 0 && l < _state.BodyBlock.MaxSize);
                    break;
                case State.Body:
                    i += ReadBody(data, i) - 1;
                    break;
                case State.Crc16:
                    if (ReadValue(data.Span[i], _state.Crc16Block, ref _state.Crc16, State.Magic))
                    {
                        var crc16 = Crc16Ccitt.Compute([_state.MagicBlock, _state.SequenceBlock, _state.TypeBlock, _state.LengthBlock, _state.BodyBlock]);
                        if (crc16 == _state.Crc16)
                        {
                            var body = _state.BodyBlock.Span[.._state.BodyBlock.Count];
                            yield return new Frame(_state.Sequence, _state.Type, body.ToArray());
                        }
                        else
                        {
                            _logger.LogError("bad crc {Expected} != {Calcualted}", _state.Crc16, crc16);
                        }
                        _state.Reset();
                    }
                    break;
            }
        }
    }

    private void ReadMagic(ReadOnlyMemory<byte> mem, int offset)
    {
        var span = mem.Span;
        if (span[offset] == _magic[0])
        {
            _state.Reset();
            _state.MagicBlock.Add(span[offset]);
        }
        else if (_state.MagicBlock.Count > 0)
        {
            _state.MagicBlock.Add(span[offset]);
            if (!_state.MagicBlock.IsFull)
                return;
            
            if (_state.MagicBlock.Span.SequenceEqual(_magic))
            {
                _state.State = State.Seq;
            }
            else
            {
                _state.State = State.Magic;
                ClearMagic();
            }
        }
    }

    private bool ReadValue<T>(byte b, MemoryBlock mem, ref T result, State nextState, Func<T, bool>? validate = null)
    {
        mem.Add(b);

        if (!mem.IsFull)
            return false;

        result = mem.As<T>();
        
        var valid = validate?.Invoke(result) ?? true;
        if (!valid)
            _logger.LogError("{State} is invalid", _state.State);
        
        _state.State = valid ? nextState : State.Magic;
        return valid;
    }
    
    private int ReadBody(ReadOnlyMemory<byte> mem, int offset)
    {
        var need = _state.Length - _state.BodyBlock.Count;
        var toCopy = Math.Min(mem.Length - offset, need);
        _state.BodyBlock.Add(mem.Span[offset..(offset + toCopy)]);

        if (_state.BodyBlock.Count == _state.Length)
            _state.State = State.Crc16;

        return toCopy;
    }
    
    private void ClearMagic()
    {
        if (_state.MagicBlock.Span[^1] == _magic[0])
        {
            _state.Reset();
            _state.MagicBlock.Add(_magic[0]);
        }
        else
        {
            _state.Reset();
        }
    }

    private enum State
    {
        Magic,
        Seq,
        Type,
        Length,
        Body,
        Crc16
    }
}