using System.Net;
using System.Net.Http.Json;

using FabTrace.Contracts;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class RootCauseTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Root_cause_analysis_detects_recipe_yield_drop()
    {
        using var bonder = Factory.CreateMachineClient("WB-01");
        var now = DateTime.UtcNow;

        // Recipe BOND-QFN32 v1: 100% yield (2 passes)
        await PostResultsAsync(
            bonder,
            TestData.Result("SN-V1-01", recipeCode: "BOND-QFN32", recipeVersion: 1, inspectedAt: now.AddHours(-3)),
            TestData.Result("SN-V1-02", recipeCode: "BOND-QFN32", recipeVersion: 1, inspectedAt: now.AddHours(-2)));

        // Recipe BOND-QFN32 v2: 0% yield (2 failures with WIRE_LIFT)
        await PostResultsAsync(
            bonder,
            TestData.Result("SN-V2-01", recipeCode: "BOND-QFN32", recipeVersion: 2, verdict: Verdict.Fail, defectCode: "WIRE_LIFT", inspectedAt: now.AddHours(-1)),
            TestData.Result("SN-V2-02", recipeCode: "BOND-QFN32", recipeVersion: 2, verdict: Verdict.Fail, defectCode: "WIRE_LIFT", inspectedAt: now));

        using var engineerClient = Factory.CreateUserClient("engineer1", Roles.Engineer);
        var url = $"api/reports/root-cause?from={Uri.EscapeDataString(now.AddDays(-1).ToString("o"))}&to={Uri.EscapeDataString(now.AddDays(1).ToString("o"))}";
        var response = await engineerClient.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var analysis = await response.Content.ReadFromJsonAsync<RootCauseAnalysisDto>(Ct);

        Assert.NotNull(analysis);
        Assert.Contains(analysis.IdentifiedRootCauses, f => f.Contains("Recipe 'BOND-QFN32' v2 shows an abnormal yield drop"));
    }

    [Fact]
    public async Task Root_cause_analysis_detects_tester_contact_resistance_drift()
    {
        using var tester = Factory.CreateMachineClient("FT-02");
        var baseDate = DateTime.UtcNow.Date.AddDays(-3);

        // Day 1: normal resistance ~ 0.05 ohm
        var res1 = TestData.Result(
            "SN-D1-01",
            processStepCode: "FINAL_TEST",
            recipeCode: "TEST-QFN32",
            recipeVersion: 1,
            operatorBadge: "OP102",
            inspectedAt: baseDate.AddHours(10),
            readings: [new TestReadingDto { ParameterName = "ContactResistance", Value = 0.050, UpperLimit = 0.150, Unit = "ohm" }]);

        // Day 2: drifting resistance ~ 0.08 ohm
        var res2 = TestData.Result(
            "SN-D2-01",
            processStepCode: "FINAL_TEST",
            recipeCode: "TEST-QFN32",
            recipeVersion: 1,
            operatorBadge: "OP102",
            inspectedAt: baseDate.AddDays(1).AddHours(10),
            readings: [new TestReadingDto { ParameterName = "ContactResistance", Value = 0.080, UpperLimit = 0.150, Unit = "ohm" }]);

        // Day 3: severe drift ~ 0.12 ohm (> 1.5x of Day 1)
        var res3 = TestData.Result(
            "SN-D3-01",
            processStepCode: "FINAL_TEST",
            recipeCode: "TEST-QFN32",
            recipeVersion: 1,
            operatorBadge: "OP102",
            inspectedAt: baseDate.AddDays(2).AddHours(10),
            readings: [new TestReadingDto { ParameterName = "ContactResistance", Value = 0.120, UpperLimit = 0.150, Unit = "ohm" }]);

        var resp = await PostResultsAsync(tester, res1, res2, res3);
        Assert.All(resp.Items, item => Assert.Equal(IngestStatus.Created, item.Status));

        using var engineerClient = Factory.CreateUserClient("engineer1", Roles.Engineer);
        var url = $"api/reports/root-cause?from={Uri.EscapeDataString(baseDate.AddDays(-1).ToString("o"))}&to={Uri.EscapeDataString(baseDate.AddDays(4).ToString("o"))}";
        var analysis = await engineerClient.GetFromJsonAsync<RootCauseAnalysisDto>(url, Ct);

        Assert.NotNull(analysis);
        Assert.NotEmpty(analysis.ParametricDrifts);
        Assert.Contains(analysis.IdentifiedRootCauses, f => f.Contains("Tester FT-02 exhibits a progressive ContactResistance drift"));
    }
}