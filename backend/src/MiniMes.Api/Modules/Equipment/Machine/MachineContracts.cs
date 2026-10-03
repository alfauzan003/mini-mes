using MiniMes.Api.Modules.Alarms.Domain;
using MiniMes.Api.Modules.Equipment.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Equipment.Machine;

/// <summary>One equipment as the simulator needs it: what it can report and which alarms it can raise or has active.</summary>
public sealed record MachineEquipmentState(
    string Code,
    OperationCode Operation,
    EquipmentStatus Status,
    IReadOnlyList<MachineParameterDto> Parameters,
    IReadOnlyList<MachineAlarmCodeDto> AlarmCodes,
    IReadOnlyList<string> ActiveAlarmCodes);

public sealed record MachineParameterDto(
    string Name,
    ParameterKind Kind,
    string Unit,
    decimal Setpoint,
    decimal Low,
    decimal High,
    string? LowAlarmCode,
    string? HighAlarmCode);

public sealed record MachineAlarmCodeDto(string Code, AlarmSeverity Severity);
