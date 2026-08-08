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
