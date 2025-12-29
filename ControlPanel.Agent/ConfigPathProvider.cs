namespace ControlPanel.Agent;

public static class ConfigPathProvider
{
    private const string AppFolder = "ControlPanel.Agent";
    private const string FileName = "settings.json";
    public static string Path { get; }

    static ConfigPathProvider()
    {
        string appDir;
        if (OperatingSystem.IsWindows())
            appDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolder);
        else if (OperatingSystem.IsLinux())
            appDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolder);
        else
            throw new PlatformNotSupportedException();
        
        Path = System.IO.Path.Combine(appDir, FileName);
        
        if (!File.Exists(Path))
            throw new Exception($"Config file not found in path: {Path}");
    }
}