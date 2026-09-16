using System.Collections.Concurrent;
using Mixline.Core;

namespace Mixline.Logging;

public sealed class AppLog : IDisposable
{
    private readonly ConcurrentQueue<LogEvent> _recent = new();
    private readonly object _fileLock = new();
    private readonly string _filePath;
    private int _recentCount;
    private bool _disposed;

    public event Action<LogEvent>? Logged;

    public AppLog()
    {
        AppPaths.EnsureCreated();
        _filePath = Path.Combine(AppPaths.Logs, $"cuebox-{DateTime.Now:yyyyMMdd}.log");
    }

    public IReadOnlyList<LogEvent> Recent() => _recent.ToArray();

    public void Debug(string category, string message) => Write(LogLevel.Debug, category, message, null);
    public void Info(string category, string message) => Write(LogLevel.Info, category, message, null);
    public void Warning(string category, string message, string? details = null) => Write(LogLevel.Warning, category, message, details);
    public void Error(string category, string message, string? details = null) => Write(LogLevel.Error, category, message, details);
    public void Error(string category, string message, Exception ex) => Write(LogLevel.Error, category, message, Sanitize(ex.ToString()));

    public string CopyDiagnostics()
    {
        var events = _recent.ToArray();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{AppInfo.Name} {AppInfo.Version}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($"64-bit: {Environment.Is64BitOperatingSystem}");
        sb.AppendLine($"Time: {DateTimeOffset.Now:O}");
        sb.AppendLine();
        foreach (var e in events)
        {
            sb.Append(e.Timestamp.ToString("HH:mm:ss.fff"));
            sb.Append(" [");
            sb.Append(e.Level);
            sb.Append("] ");
            sb.Append(e.Category);
            sb.Append(": ");
            sb.AppendLine(Redact(e.Message));
            if (!string.IsNullOrWhiteSpace(e.Details))
                sb.AppendLine(Redact(e.Details));
        }
        return sb.ToString();
    }

    private void Write(LogLevel level, string category, string message, string? details)
    {
        if (_disposed)
            return;

        var evt = new LogEvent
        {
            Level = level,
            Category = category,
            Message = Redact(message),
            Details = details is null ? null : Redact(details)
        };

        _recent.Enqueue(evt);
        if (Interlocked.Increment(ref _recentCount) > 800)
        {
            if (_recent.TryDequeue(out _))
                Interlocked.Decrement(ref _recentCount);
        }

        try
        {
            var line = $"{evt.Timestamp:O}\t{evt.Level}\t{evt.Category}\t{evt.Message}";
            if (!string.IsNullOrEmpty(evt.Details))
                line += "\t" + evt.Details.Replace('\n', ' ');
            lock (_fileLock)
                File.AppendAllText(_filePath, line + Environment.NewLine);
        }
        catch
        {
        }

        Logged?.Invoke(evt);
    }

    private static string Sanitize(string value) => Redact(value);

    private static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        value = System.Text.RegularExpressions.Regex.Replace(
            value,
            "(access_token|refresh_token|client_secret|code_verifier|authorization)=([^\\s&]+)",
            "$1=[redacted]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        value = System.Text.RegularExpressions.Regex.Replace(
            value,
            "Bearer\\s+[A-Za-z0-9._\\-]+",
            "Bearer [redacted]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return value;
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
