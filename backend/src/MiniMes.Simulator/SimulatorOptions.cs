namespace MiniMes.Simulator;

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public required string HubUrl { get; set; }
    public required string ApiKey { get; set; }
    public double TickSeconds { get; set; } = 2;
    public double DriftChancePerTick { get; set; } = 0.01;
    public double AmbientTemperature { get; set; } = 25;
    public int? RandomSeed { get; set; }
}
