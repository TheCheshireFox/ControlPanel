using IniParser;
using IniParser.Model;

namespace ControlPanel.Agent.Linux;

public static class IconLocator
{
    private const string FallbackTheme = "hicolor";
    
    private static readonly Dictionary<string, string> _appsIcons = new();
    private static readonly IconIndex _iconIndex;

    static IconLocator()
    {
        _iconIndex = new IconIndex([GetCurrentTheme(), FallbackTheme], iconSize: 32);
        RefreshCache();
    }
    
    public static string? FindIcon(string program)
    {
        var app = File.Exists(program)
            ? _appsIcons.FirstOrDefault(x => string.Equals(x.Key, program, StringComparison.InvariantCultureIgnoreCase))
            : _appsIcons.FirstOrDefault(x => Path.GetFileName(x.Key) == program);
        
        return string.IsNullOrEmpty(app.Key) ? null : app.Value;
    }

    public static void RefreshCache()
    {
        _iconIndex.Refresh();
        _appsIcons.Clear();

        var appFiles = GetApplicationPaths()
            .SelectMany(x => Directory.EnumerateFiles(x, "*.desktop", SearchOption.TopDirectoryOnly));

        foreach (var appFile in appFiles)
        {
            var data = ReadIniFile(appFile);
            if (!data.TryGetKey("Desktop Entry.Exec", out var exec) || !data.TryGetKey("Desktop Entry.Icon", out var icon))
                continue;

            if (!TryGetExecutable(exec, out var executable) || !_iconIndex.TryResolveIcon(icon, out var iconPath))
                continue;
            
            _appsIcons[executable] = iconPath;
        }
    }

    private static IEnumerable<string> GetApplicationPaths()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "share");
        var dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS") ?? "/usr/local/share:/usr/share";

        return dataDirs
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(dataHome)
            .Select(x => Path.Combine(x, "applications"))
            .Where(Directory.Exists);
    }

    private static string GetCurrentTheme()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        var kdeglobals = Path.Combine(home, ".config", "kdeglobals");
        if (!File.Exists(kdeglobals))
            return FallbackTheme;
        
        return ReadIniFile(kdeglobals).TryGetKey("Icons.Theme",  out var theme)
            ? theme
            : FallbackTheme;
    }

    private static bool TryGetExecutable(string execLine, out string executable)
    {
        var s = execLine.Trim();

        var inQuotes = false;
        int idx;

        for (idx = 0; idx < s.Length; idx++)
        {
            if (s[idx] == '"')
            {
                inQuotes = !inQuotes;
            }
            
            if (char.IsWhiteSpace(s[idx]) && !inQuotes)
                break;
        }
        
        return (executable = Which(s[..idx])!) != null;
    }
    
    private static string? Which(string program)
    {
        if (program.Contains(Path.DirectorySeparatorChar) && File.Exists(program))
            return Path.GetFullPath(program);
        
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return null;

        return pathEnv.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => Path.Combine(x, program))
            .Where(File.Exists)
            .FirstOrDefault(x => new Mono.Unix.UnixFileInfo(x).FileAccessPermissions.HasFlag(
                Mono.Unix.FileAccessPermissions.UserExecute |
                Mono.Unix.FileAccessPermissions.GroupExecute |
                Mono.Unix.FileAccessPermissions.OtherExecute));
    }
    
    private static IniData ReadIniFile(string path)
    {
        var parser =  new FileIniDataParser
        {
            Parser =
            {
                Configuration =
                {
                    SkipInvalidLines = true,
                    AllowDuplicateKeys = true,
                    OverrideDuplicateKeys = true
                }
            }
        };

        return parser.ReadFile(path);
    }
}