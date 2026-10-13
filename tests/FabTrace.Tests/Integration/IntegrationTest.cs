using System.Net;
using System.Net.Http.Json;

using FabTrace.Contracts;

namespace FabTrace.Tests.Integration;

/// <summary>Base class: every test starts with empty production tables.</summary>
public abstract class IntegrationTest(FabTraceApiFactory factory) : IAsyncLifetime
{
    protected FabTraceApiFactory Factory { get; } = factory;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await Factory.ResetProductionDataAsync();

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    protected static async Task<IngestResponse> PostBatchAsync<T>(HttpClient client, string route, params T[] items)
    {
        using var response = await client.PostAsJsonAsync(route, items, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IngestResponse>(Ct))!;
    }

    protected static Task<IngestResponse> PostResultsAsync(HttpClient client, params InspectionResultDto[] results) =>
        PostBatchAsync(client, ApiConventions.InspectionResultsRoute, results);
}

/// <summary>Valid payloads for machine WB-01; tests override only what they are about.</summary>
public static class TestData
{
    public static readonly DateTimeOffset BaseTime = new(2026, 3, 2, 9, 0, 0, TimeSpan.FromHours(8));

    public static InspectionResultDto Result(
        string serialNumber = "LOT-T1-001",
        string lotNumber = "LOT-T1",
        Guid? resultId = null,
        string processStepCode = "WIRE_BOND",
        string recipeCode = "BOND-QFN32",
        int recipeVersion = 1,
        string operatorBadge = "OP101",
        Verdict verdict = Verdict.Pass,
        string? defectCode = null,
        DateTimeOffset? inspectedAt = null,
        int cycleTimeMs = 55_000,
        IReadOnlyList<TestReadingDto>? readings = null) =>
        new()
        {
            ResultId = resultId ?? Guid.NewGuid(),
            LotNumber = lotNumber,
            SerialNumber = serialNumber,
            ProcessStepCode = processStepCode,
            RecipeCode = recipeCode,
            RecipeVersion = recipeVersion,
            OperatorBadge = operatorBadge,
            Verdict = verdict,
            DefectCode = defectCode,
            InspectedAt = inspectedAt ?? BaseTime,
            CycleTimeMs = cycleTimeMs,
            Readings = readings ??
            [
                new TestReadingDto { ParameterName = "PullStrength", Value = 8.1, LowerLimit = 5.0, Unit = "gf" },
                new TestReadingDto { ParameterName = "BallShear", Value = 31.5, LowerLimit = 20.0, Unit = "gf" },
            ],
        };

    public static AlarmDto Alarm(Guid? eventId = null, DateTimeOffset? raisedAt = null) =>
        new()
        {
            EventId = eventId ?? Guid.NewGuid(),
            AlarmCode = "WIRE_BREAK",
            Severity = AlarmSeverity.Warning,
            Message = "Wire broken at bond head",
            RaisedAt = raisedAt ?? BaseTime,
        };

    public static MachineStateEventDto State(MachineState state, int minutesAfterBase, string? reasonCode = null) =>
        new()
        {
            EventId = Guid.NewGuid(),
            State = state,
            StartedAt = BaseTime.AddMinutes(minutesAfterBase),
            ReasonCode = reasonCode,
        };
}