using System.Net;
using System.Net.Http.Json;

using FabTrace.Contracts;
using FabTrace.Core.Demo;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class TraceQueryTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Theory]
    [InlineData("api/machines")]
    [InlineData("api/lots/LOT-T1")]
    [InlineData("api/units/LOT-T1-001")]
    public async Task Trace_endpoints_require_authentication(string url)
    {
        using var client = Factory.CreateClient();
        using var response = await client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Machines_lists_the_seeded_plant_without_exposing_key_hashes()
    {
        using var client = Factory.CreateUserClient();

        var json = await client.GetStringAsync("api/machines", Ct);
        var machines = await client.GetFromJsonAsync<List<MachineDto>>("api/machines", Ct);

        Assert.Equal(DemoFactory.Machines.Select(m => m.Code).Order(), machines!.Select(m => m.Code));
        Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unit_shows_its_lot_and_every_step_in_time_order()
    {
        using var bonder = Factory.CreateMachineClient("WB-01");
        using var tester = Factory.CreateMachineClient("FT-01");
        await PostResultsAsync(tester, TestData.Result(
            processStepCode: "FINAL_TEST", recipeCode: "TEST-QFN32", operatorBadge: "OP102",
            verdict: Verdict.Fail, defectCode: "LEAKAGE_HIGH", inspectedAt: TestData.BaseTime.AddHours(2)));
        await PostResultsAsync(bonder, TestData.Result(inspectedAt: TestData.BaseTime));

        using var client = Factory.CreateUserClient();
        var unit = await client.GetFromJsonAsync<UnitTraceDto>("api/units/LOT-T1-001", Ct);

        Assert.Equal("LOT-T1", unit!.LotNumber);
        Assert.Equal("QFN32", unit.ProductCode);
        Assert.Equal(["WIRE_BOND", "FINAL_TEST"], unit.Results.Select(r => r.ProcessStepCode));
        Assert.Equal(["WB-01", "FT-01"], unit.Results.Select(r => r.MachineCode));

        var test = unit.Results[1];
        Assert.Equal(("TEST-QFN32", 1, "OP102"), (test.RecipeCode, test.RecipeVersion, test.OperatorBadge));
        Assert.Equal((Verdict.Fail, "LEAKAGE_HIGH"), (test.Verdict, test.DefectCode));
        Assert.Equal(TestData.BaseTime.AddHours(2), test.InspectedAt);
        Assert.Equal(2, test.Readings.Count);
    }

    [Fact]
    public async Task Lot_summary_counts_units_results_and_failures()
    {
        using var bonder = Factory.CreateMachineClient("WB-01");
        await PostResultsAsync(
            bonder,
            TestData.Result("LOT-T1-001"),
            TestData.Result("LOT-T1-002", verdict: Verdict.Fail, defectCode: "WIRE_LIFT"),
            TestData.Result("LOT-T1-003"));

        using var client = Factory.CreateUserClient();
        var lot = await client.GetFromJsonAsync<LotSummaryDto>("api/lots/LOT-T1", Ct);

        Assert.Equal(new LotSummaryDto("LOT-T1", "QFN32", TestData.BaseTime, UnitCount: 3, ResultCount: 3, FailCount: 1), lot);
    }

    [Theory]
    [InlineData("api/lots/NO-SUCH-LOT")]
    [InlineData("api/units/NO-SUCH-UNIT")]
    public async Task Unknown_lot_or_unit_is_not_found(string url)
    {
        using var client = Factory.CreateUserClient();

        using var response = await client.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}