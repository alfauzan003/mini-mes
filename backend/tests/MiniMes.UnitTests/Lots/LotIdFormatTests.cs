using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.Lots.LotIds;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.UnitTests.Lots;

public class LotIdFormatTests
{
    private static readonly DateOnly Oct2 = new(2026, 10, 2);

    [Theory]
    [InlineData(LotType.Raw, Polarity.Cathode, null, 1, "RC-261002-001")]
    [InlineData(LotType.Foil, Polarity.Anode, null, 12, "FA-261002-012")]
    [InlineData(LotType.Slurry, Polarity.Cathode, "MX01", 1, "SC-261002-MX01-01")]
    [InlineData(LotType.Electrode, Polarity.Cathode, "CT01", 1, "EC-261002-CT01-001")]
    public void Composes_spec_format(LotType type, Polarity p, string? eq, int seq, string expected) =>
        Assert.Equal(expected, LotIdFormat.Compose(LotIdFormat.Prefix(type, p, Oct2, eq), type, seq).Value);

    [Fact]
    public void Pancake_appends_two_digit_lane() =>
        Assert.Equal("EC-261002-CT01-001-03", LotIdFormat.Pancake("EC-261002-CT01-001", 3));

    [Theory]
    [InlineData(LotType.Slurry, 100)]
    [InlineData(LotType.Electrode, 1000)]
    public void Overflow_is_sequence_exhausted(LotType type, int seq) =>
        Assert.Equal(ErrorCodes.LotSequenceExhausted, LotIdFormat.Compose("X", type, seq).Error!.Code);

    [Theory]
    [InlineData(LotType.Slurry, 99, "X-99")]
    [InlineData(LotType.Electrode, 999, "X-999")]
    public void Last_value_before_overflow_still_composes(LotType type, int seq, string expected) =>
        Assert.Equal(expected, LotIdFormat.Compose("X", type, seq).Value);

    [Fact]
    public void Electrode_without_equipment_throws() =>
        Assert.Throws<ArgumentException>(() => LotIdFormat.Prefix(LotType.Electrode, Polarity.Cathode, Oct2, null));

    [Fact]
    public void Slurry_without_equipment_throws() =>
        Assert.Throws<ArgumentException>(() => LotIdFormat.Prefix(LotType.Slurry, Polarity.Anode, Oct2, " "));

    [Fact]
    public void Pancake_has_no_prefix() =>
        Assert.Throws<ArgumentException>(() => LotIdFormat.Prefix(LotType.Pancake, Polarity.Cathode, Oct2, "SL01"));

    [Fact]
    public void Work_order_numbers_use_three_digits()
    {
        var prefix = LotIdFormat.WorkOrderPrefix(Oct2);

        Assert.Equal("WO-261002", prefix);
        Assert.Equal("WO-261002-007", LotIdFormat.ComposeWorkOrder(prefix, 7).Value);
        Assert.Equal(ErrorCodes.LotSequenceExhausted, LotIdFormat.ComposeWorkOrder(prefix, 1000).Error!.Code);
    }
}
