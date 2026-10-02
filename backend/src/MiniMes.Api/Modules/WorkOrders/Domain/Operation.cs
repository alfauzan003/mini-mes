using MiniMes.Api.Modules.Lots.Domain;

namespace MiniMes.Api.Modules.WorkOrders.Domain;

public class Operation
{
    private Operation()
    {
    }

    public Operation(OperationCode code, string name, LotType outputLotType, string uom)
    {
        Code = code;
        Name = name;
        OutputLotType = outputLotType;
        Uom = uom;
    }

    public OperationCode Code { get; private set; }
    public string Name { get; private set; } = "";
    public LotType OutputLotType { get; private set; }
    public string Uom { get; private set; } = "";
}
