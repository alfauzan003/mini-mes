namespace MiniMes.Api.Modules.WorkOrders.Domain;

public class ProductRouteStep
{
    private ProductRouteStep()
    {
    }

    internal ProductRouteStep(Guid productId, OperationCode operation, int seq)
    {
        ProductId = productId;
        Operation = operation;
        Seq = seq;
    }

    public Guid ProductId { get; private set; }
    public OperationCode Operation { get; private set; }
    public int Seq { get; private set; }
}
