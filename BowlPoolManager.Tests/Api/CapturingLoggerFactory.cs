using Microsoft.Extensions.Logging;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Records every log call so a test can assert on what a function wrote to the log.
    /// </summary>
    internal sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<LogEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }

        /// <summary>The formatted message plus any exception text, for substring checks.</summary>
        public IEnumerable<string> AllText() =>
            Entries.Select(e => e.Message + " " + (e.Exception?.ToString() ?? string.Empty));

        internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

        private sealed class CapturingLogger : ILogger
        {
            private readonly List<LogEntry> _entries;

            public CapturingLogger(List<LogEntry> entries) => _entries = entries;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
            }
        }
    }
}
