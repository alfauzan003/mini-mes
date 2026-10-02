namespace MiniMes.Api.Modules.WorkOrders.Domain;

public class WorkOrderOperation
{
    private WorkOrderOperation()
    {
    }

    internal WorkOrderOperation(Guid workOrderId, OperationCode operation, int seq, Guid equipmentId)
    {
        WorkOrderId = workOrderId;
        Operation = operation;
        Seq = seq;
        EquipmentId = equipmentId;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; private set; }
    public OperationCode Operation { get; private set; }
    public int Seq { get; private set; }
    public Guid EquipmentId { get; private set; }

    internal void AssignEquipment(Guid equipmentId) => EquipmentId = equipmentId;
}
