using MiniMes.Api.Shared.Results;

namespace MiniMes.Api.Modules.Quality;

/// <summary>
/// Length limit for free-text reasons. A reason ends up in the lot event note (512 characters), and the longest
/// note built from it is "FAIL &lt;defect code&gt;: &lt;reason&gt;" with a 32-character defect code.
/// </summary>
public static class ReasonRules
{
    private const int NoteColumnLength = 512;
    private const int LongestDefectCodeLength = 32;

    // "FAIL " + defect code + ": " precede the reason in the failed-inspection note.
    private const int FailNoteOverhead = 5 + LongestDefectCodeLength + 2;

    public const int MaxLength = NoteColumnLength - FailNoteOverhead;

    /// <summary>A REASON_TOO_LONG error when the (already trimmed) reason exceeds <see cref="MaxLength"/>.</summary>
    public static Error? CheckLength(string reason) =>
        reason.Length <= MaxLength
            ? null
            : new Error(ErrorCodes.ReasonTooLong, $"A reason can have at most {MaxLength} characters.");
}
