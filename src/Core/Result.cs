namespace Mixline.Core;

public readonly struct Result
{
    public bool Success { get; }
    public string? Error { get; }
    public string? Details { get; }

    private Result(bool success, string? error, string? details)
    {
        Success = success;
        Error = error;
        Details = details;
    }

    public static Result Ok() => new(true, null, null);
    public static Result Fail(string error, string? details = null) => new(false, error, details);
}

public readonly struct Result<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public string? Error { get; }
    public string? Details { get; }

    private Result(bool success, T? value, string? error, string? details)
    {
        Success = success;
        Value = value;
        Error = error;
        Details = details;
    }

    public static Result<T> Ok(T value) => new(true, value, null, null);
    public static Result<T> Fail(string error, string? details = null) => new(false, default, error, details);
}
