using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using ControlPanel.Agent.Shared;

namespace ControlPanel.Agent.Windows;

internal static class IconLocator
{
    private static readonly ConcurrentDictionary<string, AudioStreamIcon> _iconCache = new();
    
    public static AudioStreamIcon FindIcon(string exePath)
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

                    return new AudioStreamIcon(bytes);
                }
                catch
                {
                    return AudioStreamIcon.Default;
                }
            });
        }
        catch
        {
            return AudioStreamIcon.Default;
        }
    }
}