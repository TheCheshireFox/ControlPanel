using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Agent.WebSocket;
using ControlPanel.Protocol;
using Microsoft.Extensions.Logging;

namespace Agent;

public class AgentService
{
    private readonly Uri _bridgeUri;
    private readonly IAudioAgent _audioAgent;
    private readonly IWebSocketFactory _webSocketFactory;
    private readonly ILogger<AgentService> _logger;
    private readonly TimeSpan _snapshotInterval = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(3);

    public AgentService(Uri bridgeUri, IAudioAgent audioAgent, IWebSocketFactory webSocketFactory, ILogger<AgentService> logger)
    {
        _bridgeUri = bridgeUri;
        _audioAgent = audioAgent;
        _logger = logger;
        _webSocketFactory = webSocketFactory;
    }
    
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var ws = _webSocketFactory.Create();

            try
            {
                _logger.LogInformation("connecting to {Uri}", _bridgeUri);
                await ws.ConnectAsync(_bridgeUri, cancellationToken);
                _logger.LogInformation("connected");

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                var sendTask = SendSnapshotsLoopAsync(ws, linkedCts.Token);
                var recvTask = ReceiveCommandsLoopAsync(ws, linkedCts.Token);
                
                await Task.WhenAny(sendTask, recvTask);
                await linkedCts.CancelAsync();

                await Task.WhenAll(sendTask, recvTask);

                _logger.LogInformation("connection ended");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "connection error");
            }

            if (cancellationToken.IsCancellationRequested)
                break;
            
            _logger.LogInformation("reconnecting in {Delay}...", _reconnectDelay);
            try
            {
                await Task.Delay(_reconnectDelay, cancellationToken);
            }
            catch (OperationCanceledException) { }
        }

        _logger.LogInformation("stopped");
    }
    
    private async Task SendSnapshotsLoopAsync(IWebSocket ws, CancellationToken cancellationToken)
    {
        var timer = new PeriodicTimer(_snapshotInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!ws.Connected)
                    break;

                var streams = await _audioAgent.GetAudioStreamsAsync(cancellationToken);
                var msg = new StreamsMessage(streams.Select(x => new BridgeAudioStream(x.Id, x.Name, x.Mute, x.Volume)).ToArray());

                var json = JsonSerializer.Serialize(msg);
                var bytes = Encoding.UTF8.GetBytes(json);

                await ws.SendAsync(bytes, cancellationToken);
            }
        }
        finally
        {
            timer.Dispose();
        }
    }
    
    private async Task ReceiveCommandsLoopAsync(IWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested && ws.Connected)
        {
            using var ms = new MemoryStream();

            WebSocketReceiveResult? result;
            do
            {
                result = await ws.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("server closed websocket");
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cancellationToken);
                    return;
                }

                ms.Write(buffer, 0, result.Count);

            } while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(ms.ToArray());
            if (string.IsNullOrWhiteSpace(json))
                continue;

            await HandleIncomingMessageAsync(json, cancellationToken);
        }
    }
    
    private async Task HandleIncomingMessageAsync(string json, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            
            var type = doc.Deserialize<BridgeMessage>()?.Type;
            if (type == null)
            {
                _logger.LogError("message without Type");
                return;
            }
            
            switch (type)
            {
                case BridgeMessageType.SetVolume:
                {
                    var msg = doc.Deserialize<SetVolumeMessage>();
                    if (msg is not null)
                    {
                        _logger.LogInformation("SetVolume {Id} -> {Volume}", msg.Id, msg.Volume);
                        await SafeCallAsync(() => _audioAgent.SetVolumeAsync(msg.Id, msg.Volume, cancellationToken));
                    }
                    break;
                }
                case BridgeMessageType.SetMute:
                {
                    var msg = doc.Deserialize<SetMuteMessage>();
                    if (msg is not null)
                    {
                        _logger.LogInformation("SetMute {Id} -> {Mute}", msg.Id, msg.Mute);
                        await SafeCallAsync(() => _audioAgent.ToggleMuteAsync(msg.Id, msg.Mute, cancellationToken));
                    }
                    break;
                }
                default:
                    _logger.LogWarning("unknown message type '{type}'", type);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[agent] failed to handle message: {ex}");
        }
    }

    private async Task SafeCallAsync(Func<Task> func)
    {
        try
        {
            await func();
        }
        catch (Exception exc)
        {
            _logger.LogError(exc, "failed to execute action");
        }
    }
}