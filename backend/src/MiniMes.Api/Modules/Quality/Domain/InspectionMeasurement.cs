namespace MiniMes.Api.Modules.Quality.Domain;

/// <summary>One measured value of an inspection. Item name, unit and limits are copied from the spec at record time.</summary>
public class InspectionMeasurement
{
    private InspectionMeasurement()
    {
    }

    internal InspectionMeasurement(Guid inspectionId, InspectionSpec spec, decimal value)
    {
        InspectionId = inspectionId;
        SpecId = spec.Id;
        ItemName = spec.ItemName;
        Unit = spec.Unit;
        Lsl = spec.Lsl;
        Usl = spec.Usl;
        Value = value;
        Judgment = spec.Judge(value);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid InspectionId { get; private set; }
    public Guid SpecId { get; private set; }
    public string ItemName { get; private set; } = "";
    public string Unit { get; private set; } = "";
    public decimal Lsl { get; private set; }
    public decimal Usl { get; private set; }
    public decimal Value { get; private set; }
    public Judgment Judgment { get; private set; }
}
