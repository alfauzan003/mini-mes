namespace MiniMes.Api.Modules.WorkOrders.Domain;

public enum Polarity
{
    Cathode,
    Anode
}

public static class PolarityExtensions
{
    public static char Letter(this Polarity polarity) => polarity switch
    {
        Polarity.Cathode => 'C',
        Polarity.Anode => 'A',
        _ => throw new ArgumentOutOfRangeException(nameof(polarity), polarity, null)
    };
}
