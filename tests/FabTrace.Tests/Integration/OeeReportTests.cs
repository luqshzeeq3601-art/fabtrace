using System.Net.Http.Json;

using FabTrace.Contracts;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class OeeReportTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Oee_report_computes_availability_performance_quality_and_oee()
    {
        using var bonder = Factory.CreateMachineClient("WB-01");
        var t0 = new DateTime(2026, 10, 2, 7, 0, 0, DateTimeKind.Utc);

        // 1. Machine states: 1 hour Running (3600s), 1 hour Idle (3600s) -> 50% Availability
        await bonder.PostAsJsonAsync(ApiConventions.MachineStatesRoute, new[]
        {
            new MachineStateEventDto { EventId = Guid.NewGuid(), State = MachineState.Running, StartedAt = t0 },
            new MachineStateEventDto { EventId = Guid.NewGuid(), State = MachineState.Idle, StartedAt = t0.AddHours(1) },
        }, Ct);

        // 2. Production: 10 units produced (9 Pass, 1 Fail) -> 90% Quality
        var results = new List<InspectionResultDto>();
        for (int i = 1; i <= 10; i++)
        {
            results.Add(TestData.Result(
                serialNumber: $"LOT-OEE-{i:D3}",
                verdict: i == 10 ? Verdict.Fail : Verdict.Pass,
                cycleTimeMs: 1000,
                inspectedAt: t0.AddMinutes(i * 5)));
        }
        await PostResultsAsync(bonder, results.ToArray());

        using var engineerClient = Factory.CreateUserClient("engineer1", Roles.Engineer);
        var url = $"api/reports/oee?from={Uri.EscapeDataString(t0.ToString("o"))}&to={Uri.EscapeDataString(t0.AddHours(2).ToString("o"))}&machineCode=WB-01";
        var items = await engineerClient.GetFromJsonAsync<List<OeeReportItemDto>>(url, Ct);

        Assert.NotNull(items);
        var wb01 = items.First(o => o.MachineCode == "WB-01");

        Assert.Equal(50.0, wb01.AvailabilityPercent);
        Assert.Equal(90.0, wb01.QualityPercent);
        Assert.True(wb01.PerformancePercent > 0.0);
        Assert.True(wb01.OeePercent > 0.0);
    }
}