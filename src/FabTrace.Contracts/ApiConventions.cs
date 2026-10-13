namespace FabTrace.Contracts;

/// <summary>Routes and header names shared by the server and every machine client.</summary>
public static class ApiConventions
{
    public const string ApiKeyHeader = "X-Api-Key";

    public const string InspectionResultsRoute = "api/ingest/inspection-results";
    public const string AlarmsRoute = "api/ingest/alarms";
    public const string MachineStatesRoute = "api/ingest/machine-states";

    /// <summary>Largest batch the server accepts in one request.</summary>
    public const int MaxBatchSize = 1000;

    public static string ClearAlarmRoute(Guid eventId) => $"{AlarmsRoute}/{eventId}/clear";
}

/// <summary>
/// Maximum text lengths. The DTO validation attributes and the database column
/// sizes both use these, so a value that passes validation always fits its column.
/// </summary>
public static class FieldLengths
{
    public const int Code = 32;
    public const int Name = 100;
    public const int Unit = 16;
    public const int Message = 200;
}