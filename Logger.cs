namespace Hariane2Mqtt;

public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Notice,
    Warning,
    Error,
    Fatal,
}

/// <summary>
/// Tiny dependency-free leveled logger. Honours the add-on `log_level` option (via the
/// LOG_LEVEL env var) and the legacy DEBUG flag. Warnings and above go to stderr.
/// </summary>
public static class Log
{
    public static LogLevel MinLevel { get; set; } = LogLevel.Info;

    public static void Configure(string? level, bool debug)
    {
        if (!string.IsNullOrWhiteSpace(level) && Enum.TryParse<LogLevel>(level, ignoreCase: true, out var parsed))
            MinLevel = parsed;
        if (debug && MinLevel > LogLevel.Debug)
            MinLevel = LogLevel.Debug;
    }

    public static void Trace(string message) => Write(LogLevel.Trace, message);
    public static void Debug(string message) => Write(LogLevel.Debug, message);
    public static void Info(string message) => Write(LogLevel.Info, message);
    public static void Notice(string message) => Write(LogLevel.Notice, message);
    public static void Warning(string message) => Write(LogLevel.Warning, message);
    public static void Error(string message) => Write(LogLevel.Error, message);
    public static void Fatal(string message) => Write(LogLevel.Fatal, message);

    private static void Write(LogLevel level, string message)
    {
        if (level < MinLevel) return;
        var writer = level >= LogLevel.Warning ? Console.Error : Console.Out;
        writer.WriteLine($"[{level.ToString().ToUpperInvariant()}] {message}");
    }
}
