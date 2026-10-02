namespace MiniMes.Api.Modules.Lots.LotIds;

/// <summary>Last issued sequence number per ID prefix; incremented by <see cref="LotIdGenerator"/> only.</summary>
public class IdSequence
{
    public string Prefix { get; private set; } = "";
    public int LastValue { get; private set; }
}
