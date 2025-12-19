using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace ControlPanel.Bridge.Framer;

public interface IFrameTransport
{
    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
}

public interface IFrameProtocol
{
    Task SendAsync(ReadOnlyMemory<byte> data, TimeSpan timeout, CancellationToken cancellationToken);
    IAsyncEnumerable<byte[]> ReadAsync(CancellationToken cancellationToken);
}

public class FrameProtocol : IFrameProtocol
{
    // ReSharper disable once InconsistentNaming
    private static readonly byte[] Magic = [0x19, 0x16];
    private const int MaxFrameSize = 64 * 1024;

    private readonly IFrameTransport _transport;
    private readonly ILogger<FrameProtocol> _logger;
    private readonly Framer _framer;
    private readonly Channel<Frame> _frames = Channel.CreateUnbounded<Frame>(new UnboundedChannelOptions { SingleWriter = true });
    private readonly Task _readerTask;
    
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource> _acks = new();
    
    private readonly SemaphoreSlim _nextSequenceLock = new (1, 1);
    private ushort _nextSequence;
    
    public FrameProtocol(IFrameTransport transport, ILogger<FrameProtocol> logger)
    {
        _transport = transport;
        _logger = logger;
        _framer = new Framer(Magic, MaxFrameSize, logger);
        _readerTask = Task.Run(async () => await ReaderTaskAsync(CancellationToken.None));
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var seq = await GetNextSequenceAsync(cancellationToken);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frame = new Frame(seq, FrameType.Data, data.ToArray());
        
        if (!_acks.TryAdd(seq, tcs))
            throw new Exception($"Sequence {seq} already exists");
        
        await _transport.WriteAsync(_framer.ToBytes(frame), cancellationToken);
        try
        {
            await tcs.Task.WaitAsync(timeout, cancellationToken);
            _logger.LogInformation("Message {Sequence} ACKed by protocol", seq);
        }
        finally
        {
            _acks.TryRemove(seq, out _);
        }
    }

    public async IAsyncEnumerable<byte[]> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var frame in _frames.Reader.ReadAllAsync(cancellationToken))
            yield return frame.Data;
    }
    
    private readonly List<byte> _uartLog = [];

    private void AppendUartLog(ReadOnlyMemory<byte> data)
    {
        _uartLog.AddRange(data.Span);

        int nl;
        while ((nl = _uartLog.IndexOf((byte)'\n')) != -1)
        {
            var str = Encoding.UTF8.GetString(_uartLog[..nl].Where(x => x >= 0x20).ToArray());
            _logger.LogInformation("UART: {Line}", str);
            _uartLog.RemoveRange(0, nl + 1);
        }
    }

    private async Task ReaderTaskAsync(CancellationToken cancellationToken)
    {
        Memory<byte> buffer = new byte[2048];
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await _transport.ReadAsync(buffer, cancellationToken);
            if (read <= 0)
                continue;

            AppendUartLog(buffer[..read]);
            
            foreach (var frame in _framer.Append(buffer[..read]))
            {
                switch (frame.Type)
                {
                    case FrameType.ACK:
                        if (_acks.TryRemove(frame.Sequence, out var tcs))
                        {
                            _logger.LogInformation("ACK: sequence {Sequence} acked", frame.Sequence);
                            tcs.TrySetResult();
                        }
                        else
                        {
                            _logger.LogWarning("ACK: sequence {Sequence} does not exist", frame.Sequence);
                        }
                        break;
                    case FrameType.Data:
                        _logger.LogInformation("New frame, sequence: {Sequence}, type: {Type}, size: {Size}", frame.Sequence, frame.Type, frame.Data.Length);
                        await _transport.WriteAsync(_framer.ToBytes(new Frame(frame.Sequence, FrameType.ACK, [])), cancellationToken);
                        await _frames.Writer.WriteAsync(frame, cancellationToken);
                        break;
                }
            }
        }
    }
    
    private async Task<ushort> GetNextSequenceAsync(CancellationToken cancellationToken)
    {
        await _nextSequenceLock.WaitAsync(cancellationToken);
        try
        {
            return _nextSequence++;
        }
        finally
        {
            _nextSequenceLock.Release();
        }
    }
}