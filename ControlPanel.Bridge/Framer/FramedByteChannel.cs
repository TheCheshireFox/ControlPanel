using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using ControlPanel.Bridge.Options;
using ControlPanel.Bridge.Transport;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;

namespace ControlPanel.Bridge.Framer;

public interface IFrameChannel
{
    Task WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
    IAsyncEnumerable<byte[]> ReadAsync(CancellationToken cancellationToken);
}

public sealed class FramedByteChannel(
    IStreamConnector connector,
    IOptions<TransportOptions> options,
    ILogger<FramedByteChannel> logger)
    : IFrameChannel, IAsyncDisposable
{
    private static readonly byte[] Magic = [0xAB, 0xBC];
    
    private readonly TimeSpan _reconnectInterval = options.Value.ReconnectInterval;
    
    private readonly AsyncLock _connectionLock = new();
    private readonly AsyncLock _readLock = new();
    private readonly AsyncLock _writeLock = new();
    private readonly Framer _framer = new(Magic, logger);
    private readonly CancellationTokenSource _cts = new();
    
    private Task _connectTask = Task.CompletedTask;
    private StreamConnection? _connection;

    public async Task WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending frame, size={Size}, data={Data}", payload.Length, Convert.ToHexString(payload.Span));
        var frame = _framer.ToBytes(payload);

        await UseStreamAsync(async stream =>
        {
            using (await _writeLock.LockAsync(cancellationToken))
                await stream.WriteAsync(frame, cancellationToken);
        }, cancellationToken);
    }

    public async IAsyncEnumerable<byte[]> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        const int maxStreamSize = 2048;

        Memory<byte> buffer = new byte[1024];
        using var pendingBytes = new MemoryStream();
        var readOffset = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var size = await UseStreamAsync(async stream =>
            {
                using (await _readLock.LockAsync(cancellationToken))
                {
                    var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                    if (bytesRead == 0)
                        throw new EndOfStreamException("Device stream ended.");

                    return bytesRead;
                }
            }, cancellationToken);

            pendingBytes.Write(buffer[..size].Span);

            ReadOnlyMemory<byte> memory = pendingBytes.GetBuffer().AsMemory(readOffset, (int)pendingBytes.Length - readOffset);
            while (TryParseFrame(ref memory, out var frame, out var consumed))
            {
                yield return frame;
                readOffset += consumed;
            }

            if (pendingBytes.Length < maxStreamSize)
                continue;

            pendingBytes.ShrinkTo((int)pendingBytes.Length - readOffset);
            readOffset = 0;
        }
    }

    private async ValueTask UseStreamAsync(Func<Stream, ValueTask> action, CancellationToken cancellationToken)
    {
        await UseStreamAsync(async stream =>
        {
            await action(stream);
            return true;
        }, cancellationToken);
    }

    private async ValueTask<T> UseStreamAsync<T>(Func<Stream, ValueTask<T>> action, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var connection = await GetConnectionAsync(cancellationToken);
                return await action(connection.Stream);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Device stream error.");
                Disconnect(cancellationToken);
            }
        }

        throw new OperationCanceledException();
    }

    private async Task<StreamConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        using (await _connectionLock.LockAsync(cts.Token))
        {
            if (_connection != null)
                return _connection;

            if (_connectTask.IsCompleted)
                _connectTask = ConnectAsync(cts.Token);

            await _connectTask;
            return _connection ?? throw new InvalidOperationException("Device stream is not connected.");
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Opening device stream...");
                _connection = await connector.ConnectAsync(cancellationToken);
                logger.LogInformation("Device stream opened.");
                return;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Unable to open device stream.");
                await Task.Delay(_reconnectInterval, cancellationToken);
            }
        }
    }

    private void Disconnect(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        using (_connectionLock.Lock(cts.Token))
        {
            _connection?.Dispose();
            _connection = null;
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

    public async ValueTask DisposeAsync()
    {
        if (_cts.IsCancellationRequested)
            return;

        await _cts.CancelAsync();
        await _connectTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        if (_connection != null)
            await _connection.DisposeAsync().AsTask().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }
}
