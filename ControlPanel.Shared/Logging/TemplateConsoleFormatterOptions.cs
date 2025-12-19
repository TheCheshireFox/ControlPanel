using Microsoft.Extensions.Logging.Console;

namespace ControlPanel.Shared.Logging;

public class TemplateConsoleFormatterOptions : ConsoleFormatterOptions
{
    public string TimeFormat { get; set; } = "dd-MM-yyyy HH:mm:ss.fff";
    public string Template { get; init; } = TemplateConsoleFormatter.DefaultTemplate;
}