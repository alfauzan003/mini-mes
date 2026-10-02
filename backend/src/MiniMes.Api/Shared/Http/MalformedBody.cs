namespace MiniMes.Api.Shared.Http;

/// <summary>Request-body shapes that deserialize without error but cannot be processed: answered with 400.</summary>
public static class MalformedBody
{
    public static bool HasNullItem<T>(IEnumerable<T?>? items) where T : class =>
        items is not null && items.Any(item => item is null);

    public static IResult Problem(string detail) =>
        TypedResults.Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest, title: "Bad request");
}
