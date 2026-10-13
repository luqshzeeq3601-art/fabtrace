namespace FabTrace.Contracts;

public sealed record MachineDto(string Code, string Name, string MachineType, string Line, bool IsActive);

public sealed record LotSummaryDto(
    string LotNumber,
    string ProductCode,
    DateTimeOffset StartedAt,
    int UnitCount,
    int ResultCount,
    int FailCount);

public sealed record UnitTraceDto(
    string SerialNumber,
    string LotNumber,
    string ProductCode,
    IReadOnlyList<UnitResultDto> Results);

public sealed record UnitResultDto(
    Guid ResultId,
    string ProcessStepCode,
    string MachineCode,
    string RecipeCode,
    int RecipeVersion,
    string OperatorBadge,
    Verdict Verdict,
    string? DefectCode,
    DateTimeOffset InspectedAt,
    IReadOnlyList<TestReadingDto> Readings);