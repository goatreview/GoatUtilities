using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Merlin
{
    public class CustomFormatterOptions : ConsoleFormatterOptions
    {
        public CustomFormatterOptions()
        {
            TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            UseUtcTimestamp = false;
        }
    }

    public class CustomFormatter : ConsoleFormatter
    {
        public CustomFormatter() : base("CustomFormatter") { }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logLevel = logEntry.LogLevel.ToString();
            string category = logEntry.Category;
            int? eventId = logEntry.EventId.Id;
            string message = logEntry.Formatter(logEntry.State, logEntry.Exception);

            textWriter.Write($"\u001b[32m{logLevel}:\u001b[0m ");
            textWriter.WriteLine($"{category}[{eventId}]: {message}");
        }
    }
}
