using System.ComponentModel.DataAnnotations;

namespace MiniMes.Api.Shared.Time;

public sealed class PlantOptions
{
    public const string SectionName = "Plant";

    [Required]
    public string TimeZone { get; set; } = "Asia/Jakarta";
}
