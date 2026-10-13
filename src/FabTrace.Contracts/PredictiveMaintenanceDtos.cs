namespace FabTrace.Contracts;

public record TimeSeriesReadingPointDto(
    DateTime Timestamp,
    double Value,
    double? LowerLimit,
    double? UpperLimit);

public record AnomalyDetectionItemDto(
    DateTime Timestamp,
    double Value,
    bool IsAnomaly,
    bool IsChangePoint,
    double AnomalyScore,
    double PValue,
    double MartingaleScore);

public record PredictiveMaintenanceReportDto(
    string MachineCode,
    string MachineName,
    string ParameterName,
    int TotalSamples,
    int AnomalyCount,
    bool ChangePointDetected,
    DateTime? FirstChangePointTime,
    double BaselineMean,
    double CurrentMean,
    double DriftPercentage,
    string HealthStatus,
    string Recommendation,
    IReadOnlyList<AnomalyDetectionItemDto> DataPoints);

public record MachineHealthSummaryDto(
    string MachineCode,
    string MachineName,
    string MachineType,
    string Line,
    IReadOnlyList<string> MonitoredParameters,
    string HealthStatus,
    int AnomalyCount,
    string SummaryMessage,
    DateTime LastAssessedAt);