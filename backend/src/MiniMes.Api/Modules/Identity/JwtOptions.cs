using System.ComponentModel.DataAnnotations;

namespace MiniMes.Api.Modules.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    [Required, MinLength(32)]
    public string Key { get; set; } = "";

    [Range(1, 24 * 30)]
    public int ExpiryHours { get; set; } = 8;
}
