using System.Net;
using System.Net.Http.Json;

using FabTrace.Contracts;

using Microsoft.Extensions.DependencyInjection;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class YieldReportTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Stored_procedure_calculates_yield_by_machine_and_recipe()
    {
        using var bonder = Factory.CreateMachineClient("WB-01");
        var now = DateTime.UtcNow;

        // Ingest 3 units: 2 Pass, 1 Fail
        await PostResultsAsync(
            bonder,
            TestData.Result("LOT-YR-001", inspectedAt: now),
            TestData.Result("LOT-YR-002", verdict: Verdict.Fail, defectCode: "WIRE_LIFT", inspectedAt: now.AddMinutes(1)),
            TestData.Result("LOT-YR-003", inspectedAt: now.AddMinutes(2)));

        using var engineerClient = Factory.CreateUserClient("engineer1", Roles.Engineer);

        var url = $"api/reports/yield?from={Uri.EscapeDataString(now.AddHours(-1).ToString("o"))}&to={Uri.EscapeDataString(now.AddHours(1).ToString("o"))}&machineCode=WB-01";
        var response = await engineerClient.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<YieldReportItemDto>>(Ct);

        Assert.NotNull(items);
        Assert.NotEmpty(items);

        var wb01Item = items.First(i => i.MachineCode == "WB-01");
        Assert.Equal(3, wb01Item.TotalUnits);
        Assert.Equal(2, wb01Item.PassedUnits);
        Assert.Equal(1, wb01Item.FailedUnits);
        Assert.Equal(66.67, wb01Item.YieldPercent);
    }

    [Fact]
    public async Task Defect_pareto_aggregates_top_failure_reasons()
    {
        using var bonder = Factory.CreateMachineClient("WB-01");
        var now = DateTime.UtcNow;

        await PostResultsAsync(
            bonder,
            TestData.Result("LOT-DP-001", verdict: Verdict.Fail, defectCode: "WIRE_LIFT", inspectedAt: now),
            TestData.Result("LOT-DP-002", verdict: Verdict.Fail, defectCode: "WIRE_LIFT", inspectedAt: now.AddMinutes(1)),
            TestData.Result("LOT-DP-003", verdict: Verdict.Fail, defectCode: "BALL_SHEAR", inspectedAt: now.AddMinutes(2)));

        using var engineerClient = Factory.CreateUserClient("engineer1", Roles.Engineer);
        var url = $"api/reports/defects?from={Uri.EscapeDataString(now.AddHours(-1).ToString("o"))}&to={Uri.EscapeDataString(now.AddHours(1).ToString("o"))}&machineCode=WB-01";
        var items = await engineerClient.GetFromJsonAsync<List<DefectParetoItemDto>>(url, Ct);

        Assert.NotNull(items);
        Assert.NotEmpty(items);

        var wireLift = items.First(d => d.DefectCode == "WIRE_LIFT");
        Assert.True(wireLift.DefectCount >= 2);
    }
}