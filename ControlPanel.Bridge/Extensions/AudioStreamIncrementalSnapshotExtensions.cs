using ControlPanel.Bridge.Protocol;

namespace ControlPanel.Bridge.Extensions;

public static class AudioStreamIncrementalSnapshotExtensions
{
    public static (UartAudioStream[] Updated, UartAudioStreamId[] Deleted) ToUartAudioStreams(this AudioStreamIncrementalSnapshot snapshot, ITextRenderer textRenderer)
    {
        var uartUpdated = snapshot.Updated
            .OrderBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
            .Select(x => new UartAudioStream(new UartAudioStreamId(x.Id.Id, x.Id.AgentId), x.Source, CreateTextSprite(x.Name, textRenderer), x.Mute, x.Volume))
            .ToArray();

        var uartDeleted = snapshot.Deleted.Select(x => new UartAudioStreamId(x.Id.Id, x.Id.AgentId)).ToArray();
        
        return (uartUpdated, uartDeleted);
    }
    
    private static UartAudioStreamNameSprite? CreateTextSprite(string? text, ITextRenderer textRenderer)
    {
        if (text == null)
            return null;
        
        var sprite = textRenderer.Render(text);
        return new UartAudioStreamNameSprite(text, sprite.Image, sprite.Width, sprite.Height);
    }
}