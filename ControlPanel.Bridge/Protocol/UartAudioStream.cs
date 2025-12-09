namespace ControlPanel.Bridge.Protocol;

public record BridgeAudioStreamIcon(string Name, byte[] Icon);

public record BridgeAudioStream(string AgentId, string Id, byte[] Rgb565A8Icon, string Name, bool Mute, double Volume);