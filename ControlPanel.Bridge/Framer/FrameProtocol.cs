using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ControlPanel.Bridge.Framer;

public interface IFrameTransport
{
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
}

public interface IFrameProtocol
{
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
    IAsyncEnumerable<byte[]> ReadAsync(CancellationToken cancellationToken);
}

public sealed class FrameProtocol(IFrameTransport transport, ILogger<FrameProtocol> logger) : IFrameProtocol
{
    // ReSharper disable once InconsistentNaming
    private static readonly byte[] Magic = [0xAB, 0xBC];

    private readonly Framer _framer = new(Magic, logger);

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending frame, size={Size}, data={Data}", data.Length, Convert.ToHexString(data.Span));
        await transport.WriteAsync(_framer.ToBytes(data), cancellationToken);
    }

    public async IAsyncEnumerable<byte[]> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        const int maxStreamSize = 2048;
        
        Memory<byte> buffer = new byte[1024];
        using var ms = new MemoryStream();
        var readOffset = 0;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var size = await transport.ReadAsync(buffer, cancellationToken);
            ms.Write(buffer[..size].Span);
            
            ReadOnlyMemory<byte> memory = ms.GetBuffer().AsMemory(readOffset, (int)ms.Length - readOffset);

            while (TryParseFrame(ref memory, out var frame, out var consumed))
            {
                yield return frame;
                readOffset += consumed;
            }

            if (ms.Length < maxStreamSize)
                continue;
            
            ms.ShrinkTo((int)ms.Length - readOffset);
            readOffset = 0;
        }
    }

    private bool TryParseFrame(ref ReadOnlyMemory<byte> memory, [NotNullWhen(true)] out byte[]? frame, out int consumed)
    {
        frame = null;
        consumed = 0;
        
        if (memory.IsEmpty)
            return false;
        
        var sequence = new ReadOnlySequence<byte>(memory);
        var reader = new SequenceReader<byte>(sequence);

        if (!_framer.TryParseFrame(ref reader, out var frameSequence))
            return false;
        
        frame = frameSequence.Value.ToArray();
        consumed = (int)reader.Consumed;
        memory = memory[consumed..];
        return true;
    }
}