namespace ControlPanel.Protocol;

public record AgentAudioStream(string Id, string Source, string Name, bool Mute, double Volume);