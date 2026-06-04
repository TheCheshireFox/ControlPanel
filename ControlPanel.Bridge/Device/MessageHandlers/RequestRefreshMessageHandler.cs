using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Bridge.Extensions;
using Mediator;

namespace ControlPanel.Bridge.DeviceMessageHandlers;

public class RequestRefreshMessageHandler(
    IAudioStreamRepository audioStreamRepository,
    IDeviceConnection connection) : INotificationHandler<RequestRefreshDeviceMessage>
{
    public async ValueTask Handle(RequestRefreshDeviceMessage deviceMessage, CancellationToken cancellationToken)
    {
        var streamsInfoAsDiff = (await audioStreamRepository.GetAllAsync(cancellationToken)).Select(AudioStreamDiff.FromStreamInfo).ToArray();
        var (updated, deleted) = new AudioStreamIncrementalSnapshot(streamsInfoAsDiff, []).ToDeviceAudioStreams();
        await connection.SendMessageAsync(new StreamsDeviceMessage(updated, deleted), cancellationToken);
    }
}
