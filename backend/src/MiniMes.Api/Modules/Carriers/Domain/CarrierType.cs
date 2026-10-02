using MiniMes.Api.Modules.Lots.Domain;

namespace MiniMes.Api.Modules.Carriers.Domain;

public class CarrierType
{
    private CarrierType()
    {
    }

    public CarrierType(string code, string name, LotType allowedLotType)
    {
        Code = code;
        Name = name;
        AllowedLotType = allowedLotType;
    }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public LotType AllowedLotType { get; private set; }
}
