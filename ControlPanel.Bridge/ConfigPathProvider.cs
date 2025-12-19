namespace ControlPanel.Bridge;

public static class ConfigPathProvider
{
    private const string AppFolder = "ControlPanel.Bridge";
    private const string FileName = "settings.json";
    public static string Path { get; }

    static ConfigPathProvider()
    {
        Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolder, FileName);
    }
}