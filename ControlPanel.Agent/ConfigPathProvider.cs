namespace ControlPanel.Agent;

public static class ConfigPathProvider
{
    private const string AppFolder = "ControlPanel.Agent";
    private const string FileName = "settings.json";
    public static string AppDir { get; }
    public static string Path { get; }

    static ConfigPathProvider()
    {
        if (OperatingSystem.IsWindows())
            AppDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolder);
        else if (OperatingSystem.IsLinux())
            AppDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolder);
        else
            throw new PlatformNotSupportedException();
        
        Path =  System.IO.Path.Combine(AppDir, FileName);
        
        if (!File.Exists(Path))
            throw new Exception($"Config file not found in path: {Path}");
    }
}