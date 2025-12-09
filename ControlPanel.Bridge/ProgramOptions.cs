using CommandLine;

namespace ControlPanel.Bridge.Options;

public class ProgramOptions
{
    [Option('p', "port", Required = false, Default = 8080)]
    public int Port { get; set; }
    
    [Option('t', "tty", Required = false, Default = "/dev/ttyUSB0")]
    public string Tty { get; set; } = string.Empty;
    
    [Option('b', "baudrate", Required = false, Default = 115200)]
    public int BaudRate { get; set; }
    
    [Option('r', "reconnect-interval", Required = false, Default = 5)]
    public int ReconnectInterval { get; set; }
}