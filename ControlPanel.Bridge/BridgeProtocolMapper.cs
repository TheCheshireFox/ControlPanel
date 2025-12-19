using ControlPanel.Bridge.Protocol;
using ControlPanel.Protocol;

namespace ControlPanel.Bridge;

public static class BridgeProtocolMapper
{
    public static SetVolumeMessage ToTransport(UartSetVolumeMessage m) => new(m.Id.Id, m.Volume);
    
    public static SetMuteMessage ToTransport(UartSetMuteMessage m) => new(m.Id.Id, m.Mute);

    public static BridgeMessage ToTransport(UartMessage m) => m switch
    {
        UartSetMuteMessage setMuteMessage => ToTransport(setMuteMessage),
        UartSetVolumeMessage setVolumeMessage => ToTransport(setVolumeMessage),
        _ => throw new Exception($"No mapping for type {m.GetType()}")
    };
}