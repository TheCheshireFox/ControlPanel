namespace ControlPanel.Protocol;

public record BridgeAudioStream(string Id, string Source, string Name, bool Mute, double Volume);