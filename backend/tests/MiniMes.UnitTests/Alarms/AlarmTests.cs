using MiniMes.Api.Modules.Alarms.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.UnitTests.AlarmsModule;

public class AlarmTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);

    private static Alarm Warning() => Alarm.Raise(
        Guid.NewGuid(), new AlarmCode("MX-VAC-LOW", "Vacuum too weak", AlarmSeverity.Warning, OperationCode.Mix), Now);

    [Fact]
    public void Raise_copies_severity_from_code()
    {
        var equipmentId = Guid.NewGuid();
        var code = new AlarmCode("CT-WEB-BREAK", "Web break", AlarmSeverity.Critical, OperationCode.Coat);

        var alarm = Alarm.Raise(equipmentId, code, Now);

        Assert.Equal(equipmentId, alarm.EquipmentId);
        Assert.Equal("CT-WEB-BREAK", alarm.AlarmCode);
        Assert.Equal(AlarmSeverity.Critical, alarm.Severity);
        Assert.Equal(Now, alarm.RaisedAt);
        Assert.True(alarm.IsActive);
    }

    [Fact]
    public void Clear_twice_is_not_active()
    {
        var alarm = Warning();
        Assert.True(alarm.Clear(Now.AddMinutes(1)).IsSuccess);

        var again = alarm.Clear(Now.AddMinutes(2));

        Assert.Equal(ErrorCodes.AlarmNotActive, again.Error!.Code);
        Assert.Equal(Now.AddMinutes(1), alarm.ClearedAt);
        Assert.False(alarm.IsActive);
    }

    [Fact]
    public void Acknowledge_twice_is_already_acknowledged()
    {
        var alarm = Warning();
        var first = Guid.NewGuid();
        Assert.True(alarm.Acknowledge(first, Now.AddMinutes(1)).IsSuccess);

        var again = alarm.Acknowledge(Guid.NewGuid(), Now.AddMinutes(2));

        Assert.Equal(ErrorCodes.AlarmAlreadyAcknowledged, again.Error!.Code);
        Assert.Equal(first, alarm.AcknowledgedById);
    }
}
