using ControlPanel.Protocol;
using Mapster;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;
using Svg.Skia;

namespace ControlPanel.Bridge.Protocol;

public class DerivedMapperSetter<TSource, TDestination>
{
    private readonly Dictionary<Type, Type> _mapping;
    private readonly TypeAdapterConfig _config;

    public DerivedMapperSetter(Dictionary<Type, Type> mapping, TypeAdapterConfig config)
    {
        _mapping = mapping;
        _config = config;
    }

    public DerivedMapperSetter<TSource, TDestination> Map<TDerivedSource, TDerivedDestination>(bool newConfig = true, Action<TypeAdapterSetter<TDerivedSource, TDerivedDestination>>? config = null)
        where TDerivedSource : class, TSource
        where TDerivedDestination : class, TDestination
    {
        _mapping.Add(typeof(TDerivedSource), typeof(TDerivedDestination));

        if (newConfig)
        {
            var c = _config.NewConfig<TDerivedSource, TDerivedDestination>();
            config?.Invoke(c);
        }

        return this;
    }
}

public static class TypeAdapterSetterExtension
{
    public static TypeAdapterSetter<TSource, TDestination> MapDerived<TSource, TDestination>(this TypeAdapterSetter<TSource, TDestination> setter, Action<DerivedMapperSetter<TSource, TDestination>> config)
    {
        var mapping = new Dictionary<Type, Type>();
        var derivedSetter = new DerivedMapperSetter<TSource, TDestination>(mapping, setter.Config);
        config(derivedSetter);
        
        setter.MapWith(src => Map<TSource, TDestination>(src, mapping));

        return setter;
    }

    private static TDestination Map<TSource, TDestination>(TSource src, Dictionary<Type, Type> mapping)
    {
        if (src == null)
            return default!;

        if (!mapping.TryGetValue(src.GetType(), out var dstType))
            throw new NotSupportedException($"Unsupported UartMessage type: {src.GetType().FullName}");

        return (TDestination?)src.Adapt(src.GetType(), dstType) ?? throw new Exception($"Failed to convert {src.GetType().FullName} to {dstType.FullName}");
    }
}

// ReSharper disable once UnusedType.Global
public class UartProtocolMapperRegister : IRegister
{
    private const int TargetIconSize = 18;

    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<BridgeMessage, UartMessage>()
            .MapDerived(m => m
                .Map<StreamsMessage, UartStreamsMessage>(config: c => c.MapToConstructor(true))
                .Map<SetVolumeMessage, UartSetVolumeMessage>(config: c => c.MapToConstructor(true))
                .Map<SetMuteMessage, UartSetMuteMessage>(config: c => c.MapToConstructor(true)));

        config.NewConfig<UartMessage, BridgeMessage>()
            .MapDerived(m => m
                .Map<UartStreamsMessage, StreamsMessage>(config: c => c.MapToConstructor(true))
                .Map<UartSetVolumeMessage, SetVolumeMessage>(config: c => c.MapToConstructor(true))
                .Map<UartSetMuteMessage, SetMuteMessage>(config: c => c.MapToConstructor(true)));

        config.NewConfig<BridgeMessageType, UartMessageType>().TwoWays();
        config.NewConfig<BridgeAudioStream, UartAudioStream>()
            .Map(dest => dest.Rgb565A8Icon, src => ToRgb565A8(src.Icon.Name, src.Icon.Icon))
            .MapToConstructor(true);
    }

    private static byte[] ToRgb565A8(string name, byte[] raw)
    {
        if (string.IsNullOrEmpty(name) || raw.Length == 0)
            return [];
        
        using var img = LoadRasterizedIcon(name, raw, TargetIconSize);
        return ConvertToRgb565A8(img);
    }
    
    private static Image<Rgba32> LoadRasterizedIcon(string name, byte[] raw, int size)
    {
        var ext = Path.GetExtension(name)?.ToLowerInvariant();

        Image<Rgba32> ret;
        
        if (ext is ".svg" or ".svgz")
        {
            using var svgStream = new MemoryStream(raw);
            using var svg = new SKSvg();
            svg.Load(svgStream);

            if (svg.Picture is null)
                throw new InvalidOperationException($"Failed to load SVG from '{name}'.");

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
    
    private static byte[] ConvertToRgb565A8(Image<Rgba32> img)
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
}