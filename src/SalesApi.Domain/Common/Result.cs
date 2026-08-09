namespace SalesApi.Domain.Common;

public sealed class Notification
{
    public string Key { get; }

    public string Message { get; }

    public Notification(string key, string message)
    {
        Key = key;
        Message = message;
    }
}

public class Result
{
    public bool IsSuccess { get; }

    public IReadOnlyCollection<Notification> Errors { get; }

    protected Result(bool isSuccess, IReadOnlyCollection<Notification> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public static Result Success() => new(true, Array.Empty<Notification>());

    public static Result Failure(Notification error) => new(false, new[] { error });

    public static Result Failure(IReadOnlyCollection<Notification> errors) => new(false, errors);
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, IReadOnlyCollection<Notification> errors)
        : base(isSuccess, errors)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, Array.Empty<Notification>());

    public static new Result<T> Failure(Notification error) => new(false, default, new[] { error });

    public static new Result<T> Failure(IReadOnlyCollection<Notification> errors) => new(false, default, errors);
}
