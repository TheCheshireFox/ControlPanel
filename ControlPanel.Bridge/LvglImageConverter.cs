using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ControlPanel.Bridge;

public static class LvglImageConverter
{
    public static byte[] ConvertToRgb565A8(Image<Rgba32> img)
    {
        var w = img.Width;
        var h = img.Height;
        var n = w * h;

        var data = new byte[n * 3];  // 2*N color + N alpha
        
        var alphaOffset = n * 2; 

        img.ProcessPixelRows(accessor =>
        {
            var span = data.AsSpan();
            var idx = 0;

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
                    buf[idx++] = p.A;
                }
            }
        });

        return buf;
    }
}