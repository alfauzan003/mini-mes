namespace MiniMes.Api.Modules.Carriers.Domain;

public class Carrier
{
    private Carrier()
    {
    }

    public Carrier(string code, CarrierType type)
    {
        Code = code;
        Type = type;
        TypeCode = type.Code;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = "";
    public string TypeCode { get; private set; } = "";
    public CarrierType Type { get; private set; } = null!;
    public CarrierStatus Status { get; private set; } = CarrierStatus.Empty;
    public Guid? CurrentLotId { get; private set; }
    public uint Version { get; private set; }
}
