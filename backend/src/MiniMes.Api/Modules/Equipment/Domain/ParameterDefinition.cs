using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Equipment.Domain;

/// <summary>A process value an operation's equipment reports, with its normal band and the alarms that fire outside it.</summary>
public class ParameterDefinition
{
    private ParameterDefinition()
    {
    }

    public ParameterDefinition(
        OperationCode operation, string name, ParameterKind kind, string unit,
        decimal setpoint, decimal low, decimal high, int seq, string? lowAlarmCode, string? highAlarmCode)
    {
        Operation = operation;
        Name = name;
        Kind = kind;
        Unit = unit;
        Setpoint = setpoint;
        Low = low;
        High = high;
        Seq = seq;
        LowAlarmCode = lowAlarmCode;
        HighAlarmCode = highAlarmCode;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public OperationCode Operation { get; private set; }
    public string Name { get; private set; } = "";
    public ParameterKind Kind { get; private set; }
    public string Unit { get; private set; } = "";
    public decimal Setpoint { get; private set; }
    public decimal Low { get; private set; }
    public decimal High { get; private set; }
    public int Seq { get; private set; }
    public string? LowAlarmCode { get; private set; }
    public string? HighAlarmCode { get; private set; }
}
