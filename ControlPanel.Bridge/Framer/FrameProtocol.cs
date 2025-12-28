using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ControlPanel.Shared;
using Nito.AsyncEx;

namespace ControlPanel.Bridge.Framer;

internal static class AsyncMonitorExtensions
{
    public static async Task WaitForAsync(this AsyncMonitor monitor, Func<bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            while (!predicate())
            {
                await monitor.WaitAsync(cts.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }
}

public interface IFrameTransport
{
    event Func<CancellationToken, Task> OnReconnectedAsync;
    
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
}

public interface IFrameProtocol
{
    Task SendAsync(ReadOnlyMemory<byte> data, TimeSpan timeout, int retryCount, CancellationToken cancellationToken);
    IAsyncEnumerable<byte[]> ReadAsync(CancellationToken cancellationToken);
}

public sealed class FrameProtocol : IFrameProtocol, IAsyncDisposable
{
    // ReSharper disable once InconsistentNaming
    private static readonly byte[] Magic = [0x19, 0x16];

    private readonly IFrameTransport _transport;
    private readonly ILogger<FrameProtocol> _logger;
    private readonly Framer _framer;
    private readonly Channel<Frame> _frames = Channel.CreateUnbounded<Frame>(new UnboundedChannelOptions { SingleWriter = true });
    private readonly CancellableTask _readerTask;
    private readonly AsyncMonitor _sendSync = new();

    private ushort _nextSequence;
    private ushort _lastAckSequence = ushort.MaxValue;
    private ushort _lastReadSequence;
    
    public FrameProtocol(IFrameTransport transport, ILogger<FrameProtocol> logger)
    {
        _transport = transport;
        _logger = logger;
        _framer = new Framer(Magic, logger);
        
        _transport.OnReconnectedAsync += TransportOnOnReconnectedAsync;
        _readerTask = new CancellableTask(async ct => await TransportReaderTaskAsync(ct));
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, TimeSpan timeout, int retryCount, CancellationToken cancellationToken)
    {
        using (await _sendSync.EnterAsync(cancellationToken))
        {
            var frame = new Frame(++_nextSequence, FrameType.Data, data.ToArray());
            
            for (var i = 0; i < retryCount; i++)
            {
                try
                {
                    await SendFrameAsync(frame, cancellationToken);
                    await _sendSync.WaitForAsync(() => _lastAckSequence == frame.Sequence, timeout, cancellationToken);
                    _logger.LogDebug("Message {Sequence} ACKed", frame.Sequence);
                    return;
                }
                catch (TimeoutException) when (i < retryCount)
                {
                    _logger.LogWarning("Message {Sequence} timed out. Retry {Retry} of {MaxRetry}", frame.Sequence, i + 1, retryCount);
                }
            }
        }
    }

    private async Task SendFrameAsync(Frame frame, CancellationToken cancellationToken)
    {
        var buffer = new byte[_framer.GetFrameSize(frame.Data.Length)];
        var size = _framer.ToBytes(frame, buffer);

        await _transport.WriteAsync(buffer.AsMemory()[..size], cancellationToken);
    }
    
    public async IAsyncEnumerable<byte[]> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var frame in _frames.Reader.ReadAllAsync(cancellationToken))
            yield return frame.Data;
    }

    private async Task TransportReaderTaskAsync(CancellationToken cancellationToken)
    {
        const long streamMaxSize = 64 * 1024;
        
        Memory<byte> buffer = new byte[2048];
        var ms = new MemoryStream();
        var readOffset = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var size = await _transport.ReadAsync(buffer, cancellationToken);
                ms.Write(buffer[..size].Span);

                var memory = ms.GetBuffer().AsMemory(readOffset, (int)ms.Length - readOffset);
                var (frames, consumed) = ParseFrames(memory);
                readOffset += (int)consumed;
                
                await ProcessFramesAsync(frames, cancellationToken);

                if (ms.Length > streamMaxSize)
                {
                    ms.ShrinkTo((int)ms.Length - readOffset);
                    readOffset = 0;
                }
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Failed to process frames");
        }
    }

    private (IEnumerable<Frame> Frames, long BytesParsed) ParseFrames(ReadOnlyMemory<byte> memory)
    {
        var sequence = new ReadOnlySequence<byte>(memory);
        var reader = new SequenceReader<byte>(sequence);

        var frames = new List<Frame>();
                
        while (_framer.TryParseFrame(ref reader, out var frame))
            frames.Add(frame);
        
        return (frames, reader.Consumed);
    }
    
    private async Task ProcessFramesAsync(IEnumerable<Frame> frames, CancellationToken cancellationToken)
    {
        foreach (var frame in frames)
        {
            switch (frame.Type)
            {
                case FrameType.ACK:
                    await ProcessAckFrameAsync(frame, cancellationToken);
                    break;
                case FrameType.Data:
                    await ProcessDataFrameAsync(frame, cancellationToken);
                    break;
                default:
                    _logger.LogError("Unknown frame type {FrameType}", frame.Type);
                    break;
            }
        }
    }

    private async Task ProcessAckFrameAsync(Frame frame, CancellationToken cancellationToken)
    {
        using (await _sendSync.EnterAsync(cancellationToken))
        {
            _lastAckSequence = frame.Sequence;
            _sendSync.PulseAll();
        }
    }

    private async Task ProcessDataFrameAsync(Frame frame, CancellationToken cancellationToken)
    {
        _logger.LogDebug("New frame, sequence: {Sequence}, type: {Type}, size: {Size}", frame.Sequence, frame.Type, frame.Data.Length);

        if (frame.Sequence == _lastReadSequence && _lastReadSequence > 0)
        {
            _logger.LogDebug("Ignore duplicate frame {Sequence}", frame.Sequence);
            return;
        }

        _lastReadSequence = frame.Sequence;
        
        var ackFrame = new Frame(frame.Sequence, FrameType.ACK);
        
        await SendFrameAsync(ackFrame, cancellationToken);
        await _frames.Writer.WriteAsync(frame, cancellationToken);
    }

    private Task TransportOnOnReconnectedAsync(CancellationToken arg)
    {
        Interlocked.Exchange(ref _lastReadSequence, 0); // TODO: implement with SYN frame
        return Task.CompletedTask;
    }
    
    public async ValueTask DisposeAsync()
    {
        _transport.OnReconnectedAsync -= TransportOnOnReconnectedAsync;
        await _readerTask.DisposeAsync();
    }
}