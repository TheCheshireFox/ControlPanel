using Mediator;

namespace ControlPanel.Bridge.Audio;

public record AudioStreamIncrementalSnapshot(AudioStreamDiff[] Updated, AudioStreamInfo[] Deleted) : INotification;