using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Bridge.Device.Messaging;
using ControlPanel.Bridge.Framer;
using ControlPanel.Shared.Messaging;

namespace ControlPanel.Bridge;

public interface IDeviceConnection
{
    Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : DeviceMessage;
}

public class DeviceConnection(IFrameProtocol protocol) : IDeviceConnection, IMessageTransport<DeviceMessage>
{
    public Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : DeviceMessage
        => protocol.SendAsync(DeviceMessageSerializer.Serialize(message), cancellationToken);

    public IAsyncEnumerable<DeviceMessage> ReadAsync(CancellationToken cancellationToken)
        => protocol.ReadAsync(cancellationToken).Select(DeviceMessageSerializer.Deserialize);
}