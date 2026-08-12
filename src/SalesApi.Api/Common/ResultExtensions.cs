using SalesApi.Domain.Common;

namespace SalesApi.Api.Common;

// notFoundKeys identifica quais chaves de Notification representam "recurso não encontrado"
// (404); qualquer outra chave de falha vira 400.
public static class ResultExtensions
{
    public static IResult ToErrorResult(this Result result, params string[] notFoundKeys)
    {
        var errors = result.Errors.Select(error => new { key = error.Key, message = error.Message });

        return notFoundKeys.Length > 0 && result.Errors.Any(error => notFoundKeys.Contains(error.Key))
            ? Results.NotFound(new { errors })
            : Results.BadRequest(new { errors });
    }

    public static IResult ToHttpResult<T>(this Result<T> result, params string[] notFoundKeys)
        => result.IsSuccess ? Results.Ok(result.Value) : result.ToErrorResult(notFoundKeys);

    public static IResult ToNoContentResult(this Result result, params string[] notFoundKeys)
        => result.IsSuccess ? Results.NoContent() : result.ToErrorResult(notFoundKeys);
}
