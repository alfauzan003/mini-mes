using System.ComponentModel.DataAnnotations;

namespace MiniMes.Api.Modules.Equipment.Machine;

public sealed class MachineOptions
{
    public const string SectionName = "Machine";

    /// <summary>The shared secret equipment simulators present in the <c>X-Machine-Key</c> header.</summary>
    [Required, MinLength(16)]
    public string ApiKey { get; set; } = "";
}
