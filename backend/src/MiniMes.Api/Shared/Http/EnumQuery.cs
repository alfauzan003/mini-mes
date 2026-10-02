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

    /// <summary>
    /// Parses a comma-separated list such as <c>WAIT,RUN</c>. An absent value gives an empty list;
    /// false when any item is not a member of <typeparamref name="T"/>.
    /// </summary>
    public static bool TryParseList<T>(string? raw, out IReadOnlyList<T> values) where T : struct, Enum
    {
        var parsed = new List<T>();
        values = parsed;
        foreach (var item in (raw ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParse<T>(item, out var value) || value is null)
            {
                return false;
            }

            parsed.Add(value.Value);
        }

        return true;
    }

    public static IResult Invalid(string parameter, string? raw) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [parameter] = [$"'{raw}' is not a valid value."]
        });
}
