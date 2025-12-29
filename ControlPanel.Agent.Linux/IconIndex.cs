using System.Text.RegularExpressions;

namespace ControlPanel.Agent.Linux;

internal partial class IconIndex
{
    private record IconInfo(int Size, string Extension, string Theme, string Path);
    
    private static readonly HashSet<string> _imageExtensions = new([".svg", ".png", ".xpm"], StringComparer.InvariantCultureIgnoreCase);
    private static readonly Regex _sizeRegex = SizeRegex();
    
    private static readonly string[] _iconSearchPaths = GetIconSearchPaths();
    
    private readonly string[] _themes;
    private readonly int _iconSize;
    private readonly Dictionary<string, List<IconInfo>> _index = new();

    public IconIndex(string[] themes, int iconSize)
    {
        _themes = themes;
        _iconSize = iconSize;
    }

    public void Refresh()
    {
        _index.Clear();
        
        foreach (var searchPath in _iconSearchPaths)
        {
            foreach (var theme in _themes)
            {
                var themePath = Path.Combine(searchPath, theme);
                if (!Directory.Exists(themePath))
                    continue;
                
                foreach (var file in Directory.EnumerateFiles(themePath, "*.*", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var extension = Path.GetExtension(file);
                
                    if (!_imageExtensions.Contains(extension))
                        continue;
                
                    if (!_index.TryGetValue(name, out var list))
                        _index[name] = list = [];
                
                    var m = _sizeRegex.Match(file);

                    list.Add(new IconInfo(
                        Size: m.Success ? int.Parse(m.Groups[1].Value) : 0,
                        Extension: Path.GetExtension(file),
                        Theme: theme,
                        Path: file));
                }
            }
        }

        foreach (var (_, list) in _index)
        {
            list.Sort((x, y) => x.Size.CompareTo(y.Size));
        }
    }

    public bool TryResolveIcon(string iconName, out string iconPath)
    {
        foreach (var theme in _themes)
        {
            if (TryResolveIconByTheme(iconName, theme, out iconPath))
                return true;
        }

        iconPath = null!;
        return false;
    }

    private bool TryResolveIconByTheme(string iconName, string theme, out string iconPath)
    {
        iconPath = null!;
        
        if (!_index.TryGetValue(iconName, out var list))
            return false;
        
        var themedList = list.Where(x => x.Theme == theme)
            .ToList();

        var svgs = themedList
            .Where(x => string.Equals(x.Extension, ".svg", StringComparison.InvariantCultureIgnoreCase))
            .ToList();
        
        if ((iconPath = FindSizedIcon(svgs)!) != null)
            return true;
        
        var nonSvgs = themedList
            .Where(x => !string.Equals(x.Extension, ".svg", StringComparison.InvariantCultureIgnoreCase))
            .ToList();
        
        return (iconPath = FindSizedIcon(nonSvgs)!) != null;
    }

    private string? FindSizedIcon(List<IconInfo> sortedIcons)
    {
        var icon = sortedIcons.Find(x => x.Size >= _iconSize);
        return icon?.Path ?? sortedIcons.LastOrDefault()?.Path; 
    }
    
    private static string[] GetIconSearchPaths()
    {
        var result = new LinkedList<string>();
        
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        var dataDirs = (Environment.GetEnvironmentVariable("XDG_DATA_DIRS") ?? "/usr/local/share:/usr/share").Split(':');
        
        foreach (var baseDir in new[] { dataHome }.Concat(dataDirs))
        {
            if (Directory.Exists(Path.Combine(baseDir, "icons")))
            {
                result.AddFirst(Path.Combine(baseDir, "icons"));
            }

            if (Directory.Exists(Path.Combine(baseDir, "pixmaps")))
            {
                result.AddLast(Path.Combine(baseDir, "pixmaps"));
            }
        }

        return result.ToArray();
    }

    [GeneratedRegex(@"(\d+)(?:(?:x(?:\d+))*)", RegexOptions.Compiled)]
    private static partial Regex SizeRegex();
}