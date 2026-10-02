namespace MiniMes.Api.Shared.Http;

/// <summary>
/// Parses query-string enum filters. Accepts the SCREAMING_SNAKE values the JSON uses
/// (and any casing), because minimal API enum binding only accepts the C# member names.
/// </summary>
public static class EnumQuery
{
    /// <summary>True when <paramref name="raw"/> is absent (value null) or names a member of <typeparamref name="T"/>.</summary>
    public static bool TryParse<T>(string? raw, out T? value) where T : struct, Enum
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var normalized = raw.Trim().Replace("_", "");
        if (Enum.TryParse<T>(normalized, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    public static IResult Invalid(string parameter, string? raw) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [parameter] = [$"'{raw}' is not a valid value."]
        });
}
