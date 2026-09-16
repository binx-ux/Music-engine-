namespace Mixline.Logging;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

public sealed class LogEvent
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public LogLevel Level { get; init; }
    public string Category { get; init; } = "";
    public string Message { get; init; } = "";
    public string? Details { get; init; }
}
