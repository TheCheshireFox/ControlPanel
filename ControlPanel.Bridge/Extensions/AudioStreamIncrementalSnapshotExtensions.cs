using ControlPanel.Bridge.Protocol;

namespace ControlPanel.Bridge.Extensions;

public static class AudioStreamIncrementalSnapshotExtensions
{
    public static (AudioStream[] Updated, Protocol.AudioStreamId[] Deleted) ToUartAudioStreams(this AudioStreamIncrementalSnapshot snapshot, ITextRenderer textRenderer)
    {
        var uartUpdated = snapshot.Updated
            .OrderBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
            .Select(x => new AudioStream(new Protocol.AudioStreamId(x.Id.Id, x.Id.AgentId), x.Source, CreateTextSprite(x.Name, textRenderer), x.Mute, x.Volume))
            .ToArray();

        var uartDeleted = snapshot.Deleted.Select(x => new Protocol.AudioStreamId(x.Id.Id, x.Id.AgentId)).ToArray();
        
        return (uartUpdated, uartDeleted);
    }
    
    private static AudioStreamNameSprite? CreateTextSprite(string? text, ITextRenderer textRenderer)
    {
        if (text == null)
            return null;
        
        var sprite = textRenderer.Render(text);
        return new AudioStreamNameSprite(text, sprite.Image, sprite.Width, sprite.Height);
    }
}