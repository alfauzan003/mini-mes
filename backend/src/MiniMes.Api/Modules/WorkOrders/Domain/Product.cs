namespace MiniMes.Api.Modules.WorkOrders.Domain;

public class Product
{
    private const int SeqStep = 10;

    private readonly List<ProductRouteStep> _route = [];

    private Product()
    {
    }

    public Product(string code, string name, Polarity polarity)
    {
        Code = code;
        Name = name;
        Polarity = polarity;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public Polarity Polarity { get; private set; }

    /// <summary>Route steps ordered by <see cref="ProductRouteStep.Seq"/>.</summary>
    public IReadOnlyList<ProductRouteStep> Route => Ordered();

    public OperationCode FirstOperation =>
        Route.Count > 0
            ? Route[0].Operation
            : throw new InvalidOperationException($"Product {Code} has no route.");

    /// <summary>Builds a product whose route runs the given operations in order, numbered 10, 20, 30, ...</summary>
    public static Product Create(string code, string name, Polarity polarity, params OperationCode[] route)
    {
        var product = new Product(code, name, polarity);
        for (var i = 0; i < route.Length; i++)
        {
            product.AddStep(route[i], (i + 1) * SeqStep);
        }

        return product;
    }

    public void AddStep(OperationCode operation, int seq) => _route.Add(new ProductRouteStep(Id, operation, seq));

    /// <summary>The operation after <paramref name="op"/> on this route, or null after the last step.</summary>
    public OperationCode? NextAfter(OperationCode op)
    {
        var route = Ordered();
        var index = route.FindIndex(s => s.Operation == op);
        if (index < 0)
        {
            throw new ArgumentException($"Operation {op} is not on the route of product {Code}.", nameof(op));
        }

        return index + 1 < route.Count ? route[index + 1].Operation : null;
    }

    public bool IsFinal(OperationCode op) => NextAfter(op) is null;

    private List<ProductRouteStep> Ordered() => _route.OrderBy(s => s.Seq).ToList();
}
