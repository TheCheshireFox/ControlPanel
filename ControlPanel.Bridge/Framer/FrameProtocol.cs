using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ControlPanel.Shared;
using Nito.AsyncEx;

namespace ControlPanel.Bridge.Framer;

public interface IFrameTransport
{
    event Func<CancellationToken, Task> OnReconnectedAsync;
    
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
}

public interface IFrameProtocol
{
    Task SendAsync(ReadOnlyMemory<byte> data, TimeSpan timeout, int retryCount, TimeSpan retryDelay, CancellationToken cancellationToken);
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
    private readonly AsyncAutoResetEvent _sendingReadyEvent = new();
    
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource> _acks = new();

    private ulong _nextSequence;
    private ulong _currentSequence = 1;
    private ulong _lastReadSequence;
    
    public FrameProtocol(IFrameTransport transport, ILogger<FrameProtocol> logger)
    {
        _transport = transport;
        _logger = logger;
        _framer = new Framer(Magic, logger);
        
        _transport.OnReconnectedAsync += TransportOnOnReconnectedAsync;
        _readerTask = new CancellableTask(async ct => await TransportReaderTaskAsync(ct));
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, TimeSpan timeout, int retryCount, TimeSpan retryDelay, CancellationToken cancellationToken)
    {
        var (frame, tcs) = PrepareFrame(data);
        
        await WaitForSendingAsync(frame.Sequence, cancellationToken);
        
        try
        {
            for (var i = 0; i < retryCount; i++)
            {
                await SendFrameAsync(frame, cancellationToken);
                
                try
                {
                    await tcs.Task.WaitAsync(timeout, cancellationToken);
                    _logger.LogDebug("Message {Sequence} ACKed", frame.Sequence);
                    return;
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Message {Sequence} timed out. Retry {Retry} of {MaxRetry}, waiting {Delay}", frame.Sequence, i + 1, retryCount, retryDelay);
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }

            throw new TimeoutException("ACK wait timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending ack");
            throw;
        }
        finally
        {
            _acks.TryRemove(frame.Sequence, out _);
            
            Interlocked.Increment(ref _currentSequence);
            _sendingReadyEvent.Set();
        }
    }

    private async Task WaitForSendingAsync(ushort seq, CancellationToken cancellationToken)
    {
        if (Interlocked.Read(ref _currentSequence) == seq)
            return;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            await _sendingReadyEvent.WaitAsync(cancellationToken);
            
            if (Interlocked.Read(ref _currentSequence) == seq)
                return;
        }
    }
    
    private (Frame Frame, TaskCompletionSource Tcs) PrepareFrame(ReadOnlyMemory<byte> data)
    {
        var seq = (ushort)Interlocked.Increment(ref _nextSequence);
         
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_acks.TryAdd(seq, tcs))
            throw new Exception($"Sequence {seq} already exists");
            
        var frame = new Frame(seq, FrameType.Data, data.ToArray());
            
        return (frame, tcs);
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
                    ProcessAckFrame(frame);
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

    private void ProcessAckFrame(Frame frame)
    {
        if (_acks.TryRemove(frame.Sequence, out var tcs))
        {
            _logger.LogDebug("Sequence {Sequence} acked", frame.Sequence);
            tcs.TrySetResult();
        }
        else
        {
            _logger.LogWarning("Sequence {Sequence} does not exist", frame.Sequence);
        }
    }

    private async Task ProcessDataFrameAsync(Frame frame, CancellationToken cancellationToken)
    {
        _logger.LogDebug("New frame, sequence: {Sequence}, type: {Type}, size: {Size}", frame.Sequence, frame.Type, frame.Data.Length);
                
        var ackFrame = new Frame(frame.Sequence, FrameType.ACK);
        await SendFrameAsync(ackFrame, cancellationToken);

        var lastReadSequence = Interlocked.Read(ref _lastReadSequence);
                    
        if (frame.Sequence > lastReadSequence || (frame.Sequence == 0 && lastReadSequence is ushort.MaxValue or 0))
        {
            Interlocked.CompareExchange(ref _lastReadSequence, frame.Sequence, lastReadSequence);
            await _frames.Writer.WriteAsync(frame, cancellationToken);
        }
        else if (frame.Sequence < lastReadSequence) // frame.Sequence == lastReadSequence is a retry, no need to spam logs with it
        {
            _logger.LogWarning("Sequence {Sequence} past last acked sequence {PastSequence}, skipping...", frame.Sequence, lastReadSequence);
        }
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