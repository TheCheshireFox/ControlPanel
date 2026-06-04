using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Bridge.Framer;
using ControlPanel.Shared.Messaging;

namespace ControlPanel.Bridge.Device.Messaging;

public class DeviceMessageTransport(IFrameProtocol frameProtocol) : IMessageTransport<DeviceMessage>
{
    public IAsyncEnumerable<DeviceMessage> ReadAsync(CancellationToken cancellationToken)
        => frameProtocol.ReadAsync(cancellationToken).Select(DeviceMessageSerializer.Deserialize);
}