using ControlPanel.Bridge.Protocol;
using ControlPanel.Protocol;
using SetMuteMessage = ControlPanel.Bridge.Protocol.SetMuteMessage;
using SetVolumeMessage = ControlPanel.Bridge.Protocol.SetVolumeMessage;

namespace ControlPanel.Bridge;

public static class BridgeProtocolMapper
{
    public static ControlPanel.Protocol.SetVolumeMessage ToTransport(SetVolumeMessage m) => new(m.Id.Id, m.Volume);
    
    public static ControlPanel.Protocol.SetMuteMessage ToTransport(SetMuteMessage m) => new(m.Id.Id, m.Mute);

    public static BridgeMessage ToTransport(Message m) => m switch
    {
        SetMuteMessage setMuteMessage => ToTransport(setMuteMessage),
        SetVolumeMessage setVolumeMessage => ToTransport(setVolumeMessage),
        _ => throw new Exception($"No mapping for type {m.GetType()}")
    };
}