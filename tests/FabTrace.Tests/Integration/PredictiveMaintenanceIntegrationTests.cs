using System.Net;
using System.Net.Http.Json;

using FabTrace.Contracts;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class PredictiveMaintenanceIntegrationTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Predictive_maintenance_detects_planted_socket_wear_change_point_on_FT02()
    {
        using var tester = Factory.CreateMachineClient("FT-02");
        var baseTime = DateTime.UtcNow.Date.AddDays(-10);

        // 20 normal results (baseline ~ 35.0 mOhm)
        var results = new List<InspectionResultDto>();
        for (var i = 0; i < 20; i++)
        {
            results.Add(TestData.Result(
                serialNumber: $"SN-NORM-{i:D3}",
                processStepCode: "FINAL_TEST",
                recipeCode: "TEST-QFN32",
                recipeVersion: 1,
                operatorBadge: "OP102",
                inspectedAt: baseTime.AddHours(i * 2),
                readings:
                [
                    new TestReadingDto { ParameterName = "ContactResistance", Value = 35.0 + (i % 3) * 0.5, UpperLimit = 50.0, Unit = "mOhm" },
                    new TestReadingDto { ParameterName = "LeakageCurrent", Value = 0.50, UpperLimit = 1.0, Unit = "uA" },
                ]));
        }

        // 20 drifting results (increasing to 55.0 mOhm)
        for (var i = 0; i < 20; i++)
        {
            results.Add(TestData.Result(
                serialNumber: $"SN-DRIFT-{i:D3}",
                processStepCode: "FINAL_TEST",
                recipeCode: "TEST-QFN32",
                recipeVersion: 1,
                operatorBadge: "OP102",
                inspectedAt: baseTime.AddHours(40 + i * 2),
                readings:
                [
                    new TestReadingDto { ParameterName = "ContactResistance", Value = 42.0 + (i * 0.8), UpperLimit = 50.0, Unit = "mOhm" },
                    new TestReadingDto { ParameterName = "LeakageCurrent", Value = 0.52, UpperLimit = 1.0, Unit = "uA" },
                ]));
        }

        var ingestResp = await PostResultsAsync(tester, results.ToArray());
        Assert.All(ingestResp.Items, item => Assert.Equal(IngestStatus.Created, item.Status));

        using var engineerClient = Factory.CreateUserClient("engineer1", Roles.Engineer);
        var url = $"api/maintenance/predictive/machine/FT-02?parameterName=ContactResistance&from={Uri.EscapeDataString(baseTime.AddDays(-1).ToString("o"))}&to={Uri.EscapeDataString(baseTime.AddDays(15).ToString("o"))}";

        var response = await engineerClient.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await response.Content.ReadFromJsonAsync<PredictiveMaintenanceReportDto>(Ct);
        Assert.NotNull(report);
        Assert.Equal("FT-02", report.MachineCode);
        Assert.Equal("ContactResistance", report.ParameterName);
        Assert.Equal(40, report.TotalSamples);
        Assert.True(report.ChangePointDetected, "ML.NET should detect the structural change point in ContactResistance");
        Assert.NotNull(report.FirstChangePointTime);
        Assert.True(report.HealthStatus is "Critical" or "Degrading");
        Assert.Contains("socket wear", report.Recommendation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(report.DataPoints, p => p.IsChangePoint);
    }

    [Fact]
    public async Task Predictive_maintenance_reports_healthy_status_for_stable_machine()
    {
        using var bonder = Factory.CreateMachineClient("WB-01");
        var baseTime = DateTime.UtcNow.Date.AddDays(-5);

        // 25 stable readings with natural Gaussian-like variance around 8.0 gf
        var results = new List<InspectionResultDto>();
        for (var i = 0; i < 25; i++)
        {
            results.Add(TestData.Result(
                serialNumber: $"SN-STABLE-{i:D3}",
                processStepCode: "WIRE_BOND",
                recipeCode: "BOND-QFN32",
                recipeVersion: 1,
                operatorBadge: "OP101",
                inspectedAt: baseTime.AddHours(i * 3),
                readings:
                [
                    new TestReadingDto { ParameterName = "PullStrength", Value = 8.0 + ((i % 5) - 2) * 0.1, LowerLimit = 5.0, Unit = "gf" }
                ]));
        }

        var ingestResp = await PostResultsAsync(bonder, results.ToArray());
        Assert.All(ingestResp.Items, item => Assert.Equal(IngestStatus.Created, item.Status));

        using var engineerClient = Factory.CreateUserClient("engineer1", Roles.Engineer);
        var url = $"api/maintenance/predictive/machine/WB-01?parameterName=PullStrength&from={Uri.EscapeDataString(baseTime.AddDays(-1).ToString("o"))}&to={Uri.EscapeDataString(baseTime.AddDays(10).ToString("o"))}";

        var report = await engineerClient.GetFromJsonAsync<PredictiveMaintenanceReportDto>(url, Ct);
        Assert.NotNull(report);
        Assert.Equal("Healthy", report.HealthStatus);
        Assert.False(report.ChangePointDetected);
        Assert.Contains("STABLE", report.Recommendation);
    }

    [Fact]
    public async Task Fleet_health_summary_returns_all_active_machines()
    {
        using var engineerClient = Factory.CreateUserClient("engineer1", Roles.Engineer);
        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow.AddDays(1);

        var url = $"api/maintenance/predictive/fleet-summary?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}";
        var summary = await engineerClient.GetFromJsonAsync<IReadOnlyList<MachineHealthSummaryDto>>(url, Ct);

        Assert.NotNull(summary);
        Assert.NotEmpty(summary);
        Assert.Contains(summary, m => m.MachineCode == "FT-02");
        Assert.Contains(summary, m => m.MachineCode == "WB-01");
    }

    [Fact]
    public async Task Predictive_maintenance_requires_engineer_or_admin_role()
    {
        using var operatorClient = Factory.CreateUserClient("operator1", Roles.Operator);
        var url = "api/maintenance/predictive/machine/FT-02?parameterName=ContactResistance";

        var response = await operatorClient.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var adminClient = Factory.CreateUserClient("admin1", Roles.Admin);
        var adminResponse = await adminClient.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }
}