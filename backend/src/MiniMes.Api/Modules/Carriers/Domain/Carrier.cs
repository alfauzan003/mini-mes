using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Shared.Results;

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

    public Result Load(Lot lot)
    {
        if (Status == CarrierStatus.Full)
        {
            return new Error(ErrorCodes.CarrierNotEmpty, $"Carrier {Code} is already full.");
        }

        if (Type.AllowedLotType != lot.Type)
        {
            return new Error(
                ErrorCodes.CarrierTypeMismatch,
                $"Carrier {Code} ({Type.Name}) cannot hold a {lot.Type} lot.");
        }

        Status = CarrierStatus.Full;
        CurrentLotId = lot.Id;
        lot.PlaceOnCarrier(Id);
        return Result.Success();
    }

    public void Unload()
    {
        Status = CarrierStatus.Empty;
        CurrentLotId = null;
    }
}
