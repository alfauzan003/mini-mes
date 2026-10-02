using MiniMes.Api.Modules.WorkOrders.Domain;

namespace MiniMes.UnitTests.WorkOrders;

public class ProductTests
{
    private static readonly Product Cathode = TestData.CathodeProduct();

    [Fact] public void First_operation_is_mix() => Assert.Equal(OperationCode.Mix, Cathode.FirstOperation);

    [Fact] public void Next_after_coat_is_cal() => Assert.Equal(OperationCode.Cal, Cathode.NextAfter(OperationCode.Coat));

    [Fact] public void Next_after_slit_is_null() => Assert.Null(Cathode.NextAfter(OperationCode.Slit));

    [Fact]
    public void Only_slit_is_final()
    {
        Assert.True(Cathode.IsFinal(OperationCode.Slit));
        Assert.False(Cathode.IsFinal(OperationCode.Cal));
    }

    [Fact]
    public void Route_is_ordered_by_seq_regardless_of_insertion_order()
    {
        var product = new Product("X", "X", Polarity.Anode);
        product.AddStep(OperationCode.Cal, 30);
        product.AddStep(OperationCode.Mix, 10);
        product.AddStep(OperationCode.Coat, 20);

        Assert.Equal(
            [OperationCode.Mix, OperationCode.Coat, OperationCode.Cal],
            product.Route.Select(s => s.Operation));
        Assert.Equal(OperationCode.Coat, product.NextAfter(OperationCode.Mix));
    }

    [Fact]
    public void Polarity_letter_is_c_or_a()
    {
        Assert.Equal('C', Polarity.Cathode.Letter());
        Assert.Equal('A', Polarity.Anode.Letter());
    }
}
