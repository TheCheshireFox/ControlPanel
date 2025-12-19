using System.Text;
using System.Xml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;
using Svg.Skia;

namespace ControlPanel.Bridge;

public class AgentAppIconProvider
{
    private readonly int _targetIconSize;
    private readonly int _targetAgentIconSize;
    private readonly Image<Rgba32> _emptyIcon;

    private Image<Rgba32> _agentIcon;

    public AgentAppIconProvider(int targetIconSize, int targetAgentIconSize)
    {
        _targetIconSize = targetIconSize;
        _targetAgentIconSize = targetAgentIconSize;
        
        _agentIcon = new Image<Rgba32>(targetAgentIconSize, targetAgentIconSize);
        _emptyIcon = new Image<Rgba32>(_targetIconSize, _targetIconSize, new Rgba32(0, 0, 0, 0));
    }

    public void SetAgentIcon(byte[] iconRaw)
    {
        _agentIcon = LoadSized(iconRaw, _targetAgentIconSize);
        ApplyAgentIcon(_emptyIcon);
    }
    
    public Image<Rgba32> GetAgentAppIcon(byte[] raw)
    {
        if (raw.Length == 0)
            return _emptyIcon.Clone();
        
        var img = LoadSized(raw, _targetIconSize);
        img.Mutate(ctx =>
        {
            var pos = new Point(img.Width - _agentIcon.Width, img.Height - _agentIcon.Height);
            ctx.DrawImage(_agentIcon, pos, new GraphicsOptions());
        });

        return img;
    }

    private void ApplyAgentIcon(Image<Rgba32> img)
    {
        img.Mutate(ctx =>
        {
            var pos = new Point(img.Width - _agentIcon.Width, img.Height - _agentIcon.Height);
            ctx.DrawImage(_agentIcon, pos, new GraphicsOptions());
        });
    }
    
    private static Image<Rgba32> LoadSized(byte[] raw, int size)
    {
        Image<Rgba32> ret;
        
        if (IsSvg(raw))
        {
            using var svgStream = new MemoryStream(raw);
            using var svg = new SKSvg();
            svg.Load(svgStream);

            if (svg.Picture is null)
                throw new InvalidOperationException($"Failed to load SVG.");

            var bounds = svg.Picture.CullRect;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                bounds = new SKRect(0, 0, size, size);

            var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            
            var scale = Math.Min(size / bounds.Width, size / bounds.Height);

            canvas.Scale(scale);
            canvas.Translate(
                (size / scale - bounds.Width) / 2f - bounds.Left,
                (size / scale - bounds.Height) / 2f - bounds.Top);

            canvas.DrawPicture(svg.Picture);
            canvas.Flush();

            using var snapshot = surface.Snapshot();
            using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 100);

            using var pngStream = new MemoryStream();
            encoded.SaveTo(pngStream);
            pngStream.Position = 0;

            ret = Image.Load<Rgba32>(pngStream);
        }
        else
        {
            ret = Image.Load<Rgba32>(raw);
        }
        
        ret.Mutate(x => x.Resize(size, size));
        return ret;
    }

    private static bool IsSvg(byte[] raw)
    {
        try
        {
            var xml = new XmlDocument();
            xml.LoadXml(Encoding.UTF8.GetString(raw));
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}

public static class LvglImageConverter
{
    public static byte[] ConvertToRgb565A8(Image<Rgba32> img)
    {
        var w = img.Width;
        var h = img.Height;
        var n = w * h;

        var data = new byte[n * 3];  // 2*N color + N alpha
        
        var alphaOffset = n * 2;     // where A8 goes

        img.ProcessPixelRows(accessor =>
        {
            var span = data.AsSpan();
            var idx = 0; // pixel index in scanline order

            for (var y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);

                for (var x = 0; x < w; x++, idx++)
                {
                    var p = row[x];

                    var r = (ushort)(p.R >> 3);
                    var g = (ushort)(p.G >> 2);
                    var b = (ushort)(p.B >> 3);
                    var rgb565 = (ushort)((r << 11) | (g << 5) | b);

                    var cpos = idx * 2;
                    span[cpos] = (byte)(rgb565 & 0xFF);
                    span[cpos + 1] = (byte)(rgb565 >> 8);

                    span[alphaOffset + idx] = p.A;
                }
            }
        });

        return data;
    }
    
    public static byte[] ConvertToAlpha8(Image<Rgba32> img)
    {
        var w = img.Width;
        var h = img.Height;
        var buf = new byte[w * h];

        var idx = 0;
        img.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < w; x++)
                {
                    var p = row[x];
                    buf[idx++] = p.A; // just alpha channel
                }
            }
        });

        return buf;
    }
}