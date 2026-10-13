using System.ComponentModel.DataAnnotations;

namespace FabTrace.Contracts;

/// <summary>One unit inspected or tested at one process step.</summary>
/// <remarks>
/// The machine is not in the payload: the server takes it from the API key.
/// Master data is referenced by code, never by database id.
/// </remarks>
public sealed record InspectionResultDto
{
    /// <summary>Generated once by the machine and resent unchanged on every retry.</summary>
    public required Guid ResultId { get; init; }

    [StringLength(FieldLengths.Code, MinimumLength = 1)]
    public required string LotNumber { get; init; }

    [StringLength(FieldLengths.Code, MinimumLength = 1)]
    public required string SerialNumber { get; init; }

    [StringLength(FieldLengths.Code, MinimumLength = 1)]
    public required string ProcessStepCode { get; init; }

    [StringLength(FieldLengths.Code, MinimumLength = 1)]
    public required string RecipeCode { get; init; }

    [Range(1, int.MaxValue)]
    public required int RecipeVersion { get; init; }

    [StringLength(FieldLengths.Code, MinimumLength = 1)]
    public required string OperatorBadge { get; init; }

    [EnumDataType(typeof(Verdict))]
    public required Verdict Verdict { get; init; }

    [StringLength(FieldLengths.Code)]
    public string? DefectCode { get; init; }

    public required DateTimeOffset InspectedAt { get; init; }

    [Range(0, int.MaxValue)]
    public int CycleTimeMs { get; init; }

    public IReadOnlyList<TestReadingDto> Readings { get; init; } = [];
}

/// <summary>One measurement taken during an inspection, with the limits it was judged against.</summary>
public sealed record TestReadingDto
{
    [StringLength(FieldLengths.Code, MinimumLength = 1)]
    public required string ParameterName { get; init; }

    public required double Value { get; init; }

    public double? LowerLimit { get; init; }

    public double? UpperLimit { get; init; }

    [StringLength(FieldLengths.Unit)]
    public string Unit { get; init; } = "";
}