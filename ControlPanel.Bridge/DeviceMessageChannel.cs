using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Bridge.Device.Messaging;
using ControlPanel.Bridge.Framer;
using ControlPanel.Shared.Messaging;

namespace ControlPanel.Bridge;

public interface IDeviceConnection
{
    Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : DeviceMessage;
}

public sealed class DeviceMessageChannel(IFrameChannel frames) : IDeviceConnection, IMessageTransport<DeviceMessage>
{
    public Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : DeviceMessage
        => frames.WriteAsync(DeviceMessageSerializer.Serialize(message), cancellationToken);

    public IAsyncEnumerable<DeviceMessage> ReadAsync(CancellationToken cancellationToken)
        => frames.ReadAsync(cancellationToken).Select(DeviceMessageSerializer.Deserialize);
}
