using System.Text;
using System.Text.Json;
using ControlPanel.Agent.Extensions;
using ControlPanel.Agent.Options;
using ControlPanel.Agent.Shared;
using ControlPanel.Agent.WebSocket;
using ControlPanel.Protocol;
using ControlPanel.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlPanel.Agent;

public class AgentService : BackgroundService
{
    private readonly Uri _bridgeUri;
    private readonly IAudioAgent _audioAgent;
    private readonly IWebSocketFactory _webSocketFactory;
    private readonly ITextWebSocketClientFactory _clientFactory;
    private readonly ILogger<AgentService> _logger;
    private readonly TimeSpan _snapshotInterval = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(3);

    public AgentService(IOptions<AgentServiceOptions> options, IAudioAgent audioAgent, IWebSocketFactory webSocketFactory, ITextWebSocketClientFactory clientFactory, ILogger<AgentService> logger)
    {
        _bridgeUri = new Uri($"ws://{options.Value.Address}/agents/{options.Value.AgentId}/ws");
        _audioAgent = audioAgent;
        _logger = logger;
        _webSocketFactory = webSocketFactory;
        _clientFactory = clientFactory;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var ws = _webSocketFactory.Create();

            try
            {
                _logger.LogInformation("connecting to {Uri}", _bridgeUri);
                await ws.ConnectAsync(_bridgeUri, stoppingToken);
                _logger.LogInformation("connected");

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                await SendAgentInitMessageAsync(ws,  linkedCts.Token);
                
                var sendTask = SendSnapshotsLoopAsync(ws, linkedCts.Token);
                var recvTask = ReceiveCommandsLoopAsync(ws, linkedCts.Token);
                
                await Task.WhenAny(sendTask, recvTask);
                await linkedCts.CancelAsync();

                await Task.WhenAll(sendTask, recvTask);

                _logger.LogInformation("connection ended");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "connection error");
            }

            if (stoppingToken.IsCancellationRequested)
                break;
            
            _logger.LogInformation("reconnecting in {Delay}...", _reconnectDelay);
            try
            {
                await Task.Delay(_reconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException) { }
        }

        _logger.LogInformation("stopped");
    }

    private async Task SendAgentInitMessageAsync(IWebSocket ws, CancellationToken cancellationToken)
    {
        var dsc = await _audioAgent.GetAudioAgentDescription();
        var msg = new AgentInitMessage(dsc.AgentIcon);
        await ws.SendJsonAsync(msg, cancellationToken);
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
                var msg = new StreamsMessage(streams.Select(x => new BridgeAudioStream(x.Id, x.Source, x.Name, x.Mute, x.Volume)).ToArray());
                await ws.SendJsonAsync(msg, cancellationToken);
            }
        }
        finally
        {
            timer.Dispose();
        }
    }
    
    private async Task ReceiveCommandsLoopAsync(IWebSocket ws, CancellationToken cancellationToken)
    {
        var client = _clientFactory.Create(ws);
        
        await foreach(var json in client.ReceiveAsync(cancellationToken))
            await HandleIncomingMessageAsync(ws, json, cancellationToken);
    }
    
    private async Task HandleIncomingMessageAsync(IWebSocket ws, string json, CancellationToken cancellationToken)
    {
        try
        {
            var message = BridgeMessageJsonSerializer.Deserialize(json);
            switch (message)
            {
                case SetVolumeMessage setVolume:
                    await SafeCallAsync(() => _audioAgent.SetVolumeAsync(setVolume.Id, setVolume.Volume, cancellationToken));
                    break;
                case SetMuteMessage setMute:
                    await SafeCallAsync(() => _audioAgent.ToggleMuteAsync(setMute.Id, setMute.Mute, cancellationToken));
                    break;
                case GetIconMessage getIcon:
                    await SafeCallAsync(async () =>
                    {
                        var icon = await _audioAgent.GetAudioStreamIconAsync(getIcon.Source, cancellationToken);
                        await ws.SendJsonAsync(new AudioStreamIconMessage(getIcon.Source, icon.Icon), cancellationToken);
                    });
                    break;
                default:
                    _logger.LogWarning("unknown message type '{Type}'", message.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "failed to handle message");
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