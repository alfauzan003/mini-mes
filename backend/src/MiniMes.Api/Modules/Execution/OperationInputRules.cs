using MiniMes.Api.Modules.Execution.Domain;
using MiniMes.Api.Modules.Lots.Domain;
using MiniMes.Api.Modules.WorkOrders.Domain;
using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Execution;

/// <summary>Which lots an operation accepts at track-in, and the role each plays in the run.</summary>
public static class OperationInputRules
{
    public static Result<IReadOnlyList<(Lot Lot, RunInputRole Role)>> Classify(
        OperationCode operation, IReadOnlyList<Lot> lots)
    {
        if (lots.Select(l => l.Id).Distinct().Count() != lots.Count)
        {
            return Invalid(operation, "the same lot was scanned more than once");
        }

        switch (operation)
        {
            case OperationCode.Mix when lots.Count >= 1 && lots.All(l => l.Type == LotType.Raw):
                return lots.Select(l => (l, RunInputRole.Secondary)).ToList();

            case OperationCode.Coat when lots.Count == 2:
                var foils = lots.Where(l => l.Type == LotType.Foil).ToList();
                var slurries = lots.Where(l => l.Type == LotType.Slurry).ToList();
                if (foils.Count == 1 && slurries.Count == 1)
                {
                    return new List<(Lot Lot, RunInputRole Role)>
                    {
                        (foils[0], RunInputRole.Primary),
                        (slurries[0], RunInputRole.Secondary)
                    };
                }

                break;

            case OperationCode.Cal or OperationCode.Slit when lots.Count == 1 && lots[0].Type == LotType.Electrode:
                return new List<(Lot Lot, RunInputRole Role)> { (lots[0], RunInputRole.Primary) };
        }

        return Invalid(operation, Expected(operation));
    }

    private static string Expected(OperationCode operation) => operation switch
    {
        OperationCode.Mix => "expected one or more RAW lots",
        OperationCode.Coat => "expected one FOIL lot and one SLURRY lot",
        _ => "expected one ELECTRODE lot"
    };

    private static Error Invalid(OperationCode operation, string reason) =>
        new(ErrorCodes.InvalidInputSet, $"These lots cannot be tracked into {operation}: {reason}.");
}
