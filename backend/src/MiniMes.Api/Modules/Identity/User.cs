namespace MiniMes.Api.Modules.Identity;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public string PasswordHash { get; set; } = "";
    public Role Role { get; set; }
}
