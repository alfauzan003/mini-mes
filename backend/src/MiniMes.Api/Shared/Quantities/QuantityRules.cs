using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Shared.Quantities;

/// <summary>What a quantity must satisfy to be stored: lot and run quantities are numeric(12,3) columns.</summary>
public static class QuantityRules
{
    public const int Decimals = 3;

    /// <summary>Largest value a numeric(12,3) column can hold.</summary>
    public const decimal Max = 999_999_999.999m;

    /// <summary>True when the value has at most three decimals and fits the column. Sign is checked by the caller.</summary>
    public static bool Fits(decimal qty) => qty <= Max && decimal.Round(qty, Decimals) == qty;

    /// <summary>An INVALID_QUANTITY error when <paramref name="qty"/> has too many decimals or is too large.</summary>
    public static Error? CheckFits(decimal qty, string what) =>
        Fits(qty)
            ? null
            : new Error(
                ErrorCodes.InvalidQuantity,
                $"{what} has at most {Decimals} decimals and cannot exceed {Max:N3}.");
}
