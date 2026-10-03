namespace MiniMes.Api.Modules.Equipment.Parameters;

public sealed class ParametersOptions
{
    public const string Section = "Parameters";

    /// <summary>Minimum seconds between stored samples of one equipment parameter.</summary>
    public int PersistIntervalSeconds { get; set; } = 10;

    public int RetentionDays { get; set; } = 7;

    /// <summary>Seconds the retention service waits before its first purge.</summary>
    public int RetentionInitialDelaySeconds { get; set; }
}
