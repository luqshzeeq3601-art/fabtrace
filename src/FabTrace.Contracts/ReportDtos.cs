namespace FabTrace.Contracts;

public record YieldReportItemDto(
    string MachineCode,
    string ProcessStepCode,
    string RecipeCode,
    int RecipeVersion,
    int TotalUnits,
    int PassedUnits,
    int FailedUnits,
    double YieldPercent);

public record MachineDowntimeItemDto(
    string MachineCode,
    int RunningSeconds,
    int IdleSeconds,
    int DownSeconds,
    int SetupSeconds,
    int MaintenanceSeconds,
    int TotalTrackedSeconds,
    double AvailabilityPercent);

public record OeeReportItemDto(
    string MachineCode,
    string MachineName,
    string Line,
    int RunningSeconds,
    int TotalTrackedSeconds,
    int TotalProduced,
    int GoodProduced,
    int FailedProduced,
    double AvailabilityPercent,
    double PerformancePercent,
    double QualityPercent,
    double OeePercent);

public record DefectParetoItemDto(
    string DefectCode,
    int DefectCount,
    double Percentage);

public record ParametricDriftItemDto(
    string MachineCode,
    DateOnly InspectionDate,
    int ReadingCount,
    double AvgValue,
    double MinValue,
    double MaxValue,
    double? UpperLimit);

public record RootCauseAnalysisDto(
    IReadOnlyList<YieldReportItemDto> RecipeComparisons,
    IReadOnlyList<ParametricDriftItemDto> ParametricDrifts,
    IReadOnlyList<DefectParetoItemDto> DefectPareto,
    IReadOnlyList<string> IdentifiedRootCauses);