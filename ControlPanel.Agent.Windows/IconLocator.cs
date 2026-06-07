using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using ControlPanel.Agent.Shared;
using Microsoft.Extensions.Logging;

namespace ControlPanel.Agent.Windows;

public interface IIconLocator
{
    AudioStreamIcon FindIcon(string exePath);
}

internal class IconLocator(ILogger<IconLocator> logger) : IIconLocator
{
    private readonly ConcurrentDictionary<string, AudioStreamIcon> _iconCache = new();

    public AudioStreamIcon FindIcon(string exePath)
    {
        try
        {
            return _iconCache.GetOrAdd(exePath, path =>
            {
                try
                {
                    using var icon = Icon.ExtractAssociatedIcon(path);
                    if (icon == null)
                        return AudioStreamIcon.Default;

                    using var bmp = icon.ToBitmap();
                    using var ms = new MemoryStream();
                    bmp.Save(ms, ImageFormat.Png);      // ImageSharp can read PNG
                    var bytes = ms.ToArray();

                    return AudioStreamIcon.FromBytes(bytes);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Could not load icon {exePath}", path);
                    return AudioStreamIcon.Default;
                }
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not load icon {exePath}", exePath);
            return AudioStreamIcon.Default;
        }
    }
}