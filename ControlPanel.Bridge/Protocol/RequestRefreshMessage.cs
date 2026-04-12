using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record RequestRefreshMessage()
    : Message(MessageType.RequestRefresh);