namespace MiniMes.Api.Shared.Results;

public sealed record Error(string Code, string Message, ErrorKind Kind = ErrorKind.Rule);
