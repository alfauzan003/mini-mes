using System.Globalization;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Lots.LotIds;

public static class LotIdFormat
{
    private const int WorkOrderWidth = 3;

    /// <summary>RC-261002, FA-261002, SC-261002-MX01, EC-261002-CT01.</summary>
    public static string Prefix(LotType type, Polarity polarity, DateOnly date, string? equipmentCode)
    {
        var stamp = Stamp(date);
        var letter = polarity.Letter();
        return type switch
        {
            LotType.Raw => $"R{letter}-{stamp}",
            LotType.Foil => $"F{letter}-{stamp}",
            LotType.Slurry => $"S{letter}-{stamp}-{RequireEquipment(type, equipmentCode)}",
            LotType.Electrode => $"E{letter}-{stamp}-{RequireEquipment(type, equipmentCode)}",
            _ => throw new ArgumentException(
                $"Lot type {type} has no sequence prefix; pancake IDs derive from their electrode lot.", nameof(type))
        };
    }

    public static Result<string> Compose(string prefix, LotType type, int seq)
    {
        var width = type == LotType.Slurry ? 2 : 3;
        return Compose(prefix, seq, width, $"{type} lots for {prefix}");
    }

    public static string Pancake(string electrodeLotId, int lane) =>
        $"{electrodeLotId}-{lane.ToString("00", CultureInfo.InvariantCulture)}";

    public static string WorkOrderPrefix(DateOnly date) => $"WO-{Stamp(date)}";

    public static Result<string> ComposeWorkOrder(string prefix, int seq) =>
        Compose(prefix, seq, WorkOrderWidth, $"Work orders for {prefix}");

    private static Result<string> Compose(string prefix, int seq, int width, string subject)
    {
        var limit = (int)Math.Pow(10, width);
        if (seq < 1 || seq >= limit)
        {
            return new Error(ErrorCodes.LotSequenceExhausted, $"{subject} are limited to {limit - 1} per day.");
        }

        return $"{prefix}-{seq.ToString(new string('0', width), CultureInfo.InvariantCulture)}";
    }

    private static string Stamp(DateOnly date) => date.ToString("yyMMdd", CultureInfo.InvariantCulture);

    private static string RequireEquipment(LotType type, string? equipmentCode) =>
        string.IsNullOrWhiteSpace(equipmentCode)
            ? throw new ArgumentException($"{type} lot IDs require an equipment code.", nameof(equipmentCode))
            : equipmentCode;
}
