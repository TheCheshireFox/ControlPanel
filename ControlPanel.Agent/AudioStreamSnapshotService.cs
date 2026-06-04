using ControlPanel.Agent.Messaging;
using ControlPanel.Agent.Shared;
using ControlPanel.Protocol;
using ControlPanel.WebSocket;

namespace ControlPanel.Agent;

public interface IAudioStreamSnapshotService
{
    Task RunAsync(CancellationToken cancellationToken);
}

public class AudioStreamSnapshotService(IWebSocket ws, IAudioAgent audioAgent) : IAudioStreamSnapshotService
{
    private readonly TimeSpan _snapshotInterval = TimeSpan.FromSeconds(1);
    
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_snapshotInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (!ws.Connected)
                break;

            var streams = await audioAgent.GetAudioStreamsAsync(cancellationToken);
            var msg = new StreamsMessage(streams.Select(x => new AgentAudioStream(x.Id, x.Source, x.Name, x.Mute, x.Volume, x.IconHash)).ToArray());
            await ws.SendAsync(AgentMessageSerializer.Serialize(msg), cancellationToken);
        }
    }
}