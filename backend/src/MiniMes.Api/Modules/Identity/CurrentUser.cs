namespace MiniMes.Api.Modules.Identity;

public interface ICurrentUser
{
    Guid UserId { get; }
    string Username { get; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId =>
        Guid.TryParse(Claim("sub"), out var id) ? id : throw new InvalidOperationException("No authenticated user.");

    public string Username => Claim("name") ?? throw new InvalidOperationException("No authenticated user.");

    private string? Claim(string type) => accessor.HttpContext?.User.FindFirst(type)?.Value;
}
