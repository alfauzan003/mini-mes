using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.Api.Modules.Lots.Domain;

public class Material
{
    private Material()
    {
    }

    /// <param name="kind">Only <see cref="LotType.Raw"/> or <see cref="LotType.Foil"/>.</param>
    public Material(string code, string name, LotType kind, Polarity polarity, string uom)
    {
        if (kind is not (LotType.Raw or LotType.Foil))
        {
            throw new ArgumentException("A material is either RAW or FOIL.", nameof(kind));
        }

        Code = code;
        Name = name;
        Kind = kind;
        Polarity = polarity;
        Uom = uom;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public LotType Kind { get; private set; }
    public Polarity Polarity { get; private set; }
    public string Uom { get; private set; } = "";
}
