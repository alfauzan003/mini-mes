using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Quality.Domain;

public class DefectCode
{
    private DefectCode()
    {
    }

    public DefectCode(string code, string description, OperationCode? operation)
    {
        Code = code;
        Description = description;
        Operation = operation;
    }

    public string Code { get; private set; } = "";
    public string Description { get; private set; } = "";

    /// <summary>The only operation this defect applies to; null means every operation.</summary>
    public OperationCode? Operation { get; private set; }

    public bool AppliesTo(OperationCode op) => Operation is null || Operation == op;
}
