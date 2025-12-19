using System.Globalization;
using ControlPanel.Bridge.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace ControlPanel.Bridge;

public sealed record TextSprite(int Width, int Height, byte[] Image);

public interface ITextRenderer
{
    TextSprite Render(string text);
}

public class TextRenderer : ITextRenderer
{
    private const float Pad = 1f; // padding because during rasterization font can go slightly out of bounds (pixel snapping, hinting)
    
    private readonly float _dpi;
    private readonly int _maxWidth;
    private readonly Font _font;
    private readonly IMemoryCache _spriteCache;
    private readonly MemoryCacheEntryOptions _entryOptions;

    public TextRenderer(IOptions<TextRendererOptions> options)
    {
        _dpi = options.Value.Dpi;
        _maxWidth = options.Value.MaxWidth;
        
        var fontsCollection = new FontCollection().AddSystemFonts();
        _font = fontsCollection.Get(options.Value.FontFamily, CultureInfo.InvariantCulture).CreateFont(options.Value.FontSize);
        
        _spriteCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = options.Value.CacheSizeKb * 1024
        });

        _entryOptions = new MemoryCacheEntryOptions { SlidingExpiration = options.Value.CacheExpiry };
    }
    
    public TextSprite Render(string text)
    {
        return _spriteCache.GetOrCreate(text, CreateTextSprite, _entryOptions)!;
    }

    private TextSprite CreateTextSprite(ICacheEntry entry)
    {
        var text = (string)entry.Key;
        var options = new RichTextOptions(_font)
        {
            Dpi = _dpi,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        
        text = Ellipsize(text, options, _maxWidth + Pad * 2);
        var bounds = TextMeasurer.MeasureBounds(text, options);
        
        var w = Math.Max((int)Math.Ceiling(bounds.Width + Pad * 2), 1);
        var h = Math.Max((int)Math.Ceiling(bounds.Height + Pad * 2), 1);
        
        options.Origin = new PointF(Pad - bounds.Left, Pad - bounds.Top);
        
        using var img = new Image<Rgba32>(w, h);
        img.Mutate(ctx =>
        {
            ctx.Clear(Color.Transparent);
            ctx.DrawText(options, text, Color.White);
        });

        var alpha8 = LvglImageConverter.ConvertToAlpha8(img);
        entry.Size = alpha8.Length;
        
        return new TextSprite(w, h, alpha8);
    }

    private static string Ellipsize(string text, RichTextOptions opt, float maxBoundsWidth, string ellipsis = "…")
    {
        if (string.IsNullOrEmpty(text) || TextMeasurer.MeasureBounds(text, opt).Width <= maxBoundsWidth)
            return text;

        if (TextMeasurer.MeasureBounds(ellipsis, opt).Width > maxBoundsWidth)
            return string.Empty;

        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            var candidate = text[..mid] + ellipsis;

            if (TextMeasurer.MeasureAdvance(candidate, opt).Width <= maxBoundsWidth)
                low = mid;
            else
                high = mid - 1;
        }

        var result = text[..low] + ellipsis;
        
        while (result.Length > ellipsis.Length && TextMeasurer.MeasureBounds(result, opt).Width > maxBoundsWidth)
        {
            low--;
            result = (low > 0 ? text[..low] : "") + ellipsis;
        }

        return result;
    }
}