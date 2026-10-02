namespace MiniMes.Api.Modules.Lots.Domain;

/// <summary>Records that <see cref="ParentLotId"/> was consumed to produce <see cref="ChildLotId"/> in a run.</summary>
public class GenealogyLink
{
    private GenealogyLink()
    {
    }

    public GenealogyLink(Guid parentLotId, Guid childLotId, Guid runId)
    {
        ParentLotId = parentLotId;
        ChildLotId = childLotId;
        RunId = runId;
    }

    public Guid ParentLotId { get; private set; }
    public Guid ChildLotId { get; private set; }
    public Guid RunId { get; private set; }
}
