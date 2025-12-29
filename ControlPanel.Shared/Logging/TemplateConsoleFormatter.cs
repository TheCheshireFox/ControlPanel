using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace ControlPanel.Shared.Logging;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class TemplateConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "template";
    
    public const string LogLevelName = "level";
    public const string TimestampName = "timestamp";
    public const string CategoryName = "category";
    public const string ShortCategoryName = "shortCategory";
    public const string MessageName = "message";
    public const string DefaultTemplate = $"{{{LogLevelName},5}}: {{{ShortCategoryName}}}: {{{MessageName}}}";

    private readonly string _template;
    
    public TemplateConsoleFormatter(IOptions<TemplateConsoleFormatterOptions> options) : base(FormatterName)
    {
        _template = Regex.Replace(options.Value.Template, $"{{{LogLevelName}(.*)}}", $"{{0:{options.Value.TimeFormat}}} {{1$1}}");
        _template = Regex.Replace(_template, $"{{{TimestampName}(.*)}}", "{2$1}");
        _template = Regex.Replace(_template, $"{{{CategoryName}(.*)}}", "{3$1}");
        _template = Regex.Replace(_template, $"{{{ShortCategoryName}(.*)}}", "{4$1}");
        _template = Regex.Replace(_template, $"{{{MessageName}(.*)}}", "{5$1}");
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        if (logEntry.State is BufferedLogRecord bufferedRecord)
        {
            var message = bufferedRecord.FormattedMessage ?? string.Empty;
            WriteInternal(null, textWriter, message, bufferedRecord.LogLevel, bufferedRecord.EventId.Id, bufferedRecord.Exception, logEntry.Category, bufferedRecord.Timestamp);
        }
        else
        {
            var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (logEntry.Exception == null && message == null)
            {
                return;
            }

            WriteInternal(scopeProvider, textWriter, message, logEntry.LogLevel, logEntry.EventId.Id, logEntry.Exception?.ToString(), logEntry.Category, DateTimeOffset.Now);
        }
    }

    private void WriteInternal(IExternalScopeProvider? scopeProvider, TextWriter textWriter, string message, LogLevel logLevel,
        int eventId, string? exception, string category, DateTimeOffset stamp)
    {
        var logLevelName = logLevel switch
        {
            LogLevel.Trace => "trace",
            LogLevel.Debug => "debug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "error",
            LogLevel.Critical => "crit",
            LogLevel.None => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };
        logLevelName = ColorizeLevel(logLevel, logLevelName);

        var shortCategory = category.Split('.')[^1];
        var resultMessage = string.Format(_template, DateTime.Now, logLevelName, stamp, category, shortCategory, message);
        
        if (!string.IsNullOrEmpty(exception))
            resultMessage += Environment.NewLine + exception;
        
        textWriter.WriteLine(resultMessage);
    }
    
    private static string ColorizeLevel(LogLevel level, string text)
    {
        // ANSI escape codes
        const string reset = "\e[0m";
        string color = level switch
        {
            LogLevel.Trace => "\e[90m",      // Bright Black (Gray)
            LogLevel.Debug => "\e[36m",      // Cyan
            LogLevel.Information => "\e[32m",// Green
            LogLevel.Warning => "\e[33m",    // Yellow
            LogLevel.Error => "\e[31m",      // Red
            LogLevel.Critical => "\e[97;41m",// White on Red background
            _ => ""
        };

        return string.IsNullOrEmpty(color) ? text : $"{color}{text}{reset}";
    }
}