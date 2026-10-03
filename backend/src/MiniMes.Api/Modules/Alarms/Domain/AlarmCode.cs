using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Alarms.Domain;

public class AlarmCode
{
    private AlarmCode()
    {
    }

    public AlarmCode(string code, string message, AlarmSeverity severity, OperationCode operation)
    {
        Code = code;
        Message = message;
        Severity = severity;
        Operation = operation;
    }

    public string Code { get; private set; } = "";
    public string Message { get; private set; } = "";
    public AlarmSeverity Severity { get; private set; }

    /// <summary>The only operation whose equipment can raise this alarm.</summary>
    public OperationCode Operation { get; private set; }
}
