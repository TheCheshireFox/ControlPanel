namespace ControlPanel.Bridge.Audio;

public record AudioStreamDiff(AudioStreamId Id, string Source, string? Name, bool? Mute, double? Volume, int? IconHash)
{
    public bool HasChanges => Name != null || Mute != null || Volume != null || IconHash != null;
    
    public static AudioStreamDiff FromStreamInfo(AudioStreamInfo streamInfo)
        => new(streamInfo.Id, streamInfo.Source, streamInfo.Name, streamInfo.Mute, streamInfo.Volume, streamInfo.IconHash);
}