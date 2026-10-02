namespace MiniMes.Api.Shared.Results;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null) =>
        result.IsSuccess
            ? onSuccess is null ? TypedResults.Ok(result.Value) : onSuccess(result.Value)
            : ToProblem(result.Error!);

    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : ToProblem(result.Error!);

    private static IResult ToProblem(Error error)
    {
        var status = error.Kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status422UnprocessableEntity
        };

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code });
    }
}
