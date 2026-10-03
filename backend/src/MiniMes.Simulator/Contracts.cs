namespace MiniMes.Simulator;

public enum OperationCode { Mix, Coat, Cal, Slit }

public enum EquipmentStatus { Idle, Running, Down, Maintenance }

public enum ParameterKind { Temperature, Speed, Pressure }

public enum AlarmSeverity { Warning, Major, Critical }

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

public sealed record ReadingInput(string Parameter, decimal Value);
