using System.Net.Http.Json;

using FabTrace.Contracts;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class DowntimeReportTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Downtime_report_uses_lead_to_calculate_state_durations()
    {
        using var client = Factory.CreateMachineClient("WB-02");
        var t0 = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

        var events = new List<MachineStateEventDto>
        {
            new() { EventId = Guid.NewGuid(), State = MachineState.Running, StartedAt = t0 },
            new() { EventId = Guid.NewGuid(), State = MachineState.Idle, StartedAt = t0.AddMinutes(10) },
            new() { EventId = Guid.NewGuid(), State = MachineState.Down, StartedAt = t0.AddMinutes(15), ReasonCode = "WIRE_FEED_EMPTY" }
        };

        var postResponse = await client.PostAsJsonAsync(ApiConventions.MachineStatesRoute, events, Ct);
        Assert.True(postResponse.IsSuccessStatusCode);

        using var engineerClient = Factory.CreateUserClient("engineer1", Roles.Engineer);
        var url = $"api/reports/downtime?from={Uri.EscapeDataString(t0.ToString("o"))}&to={Uri.EscapeDataString(t0.AddMinutes(30).ToString("o"))}&machineCode=WB-02";
        var items = await engineerClient.GetFromJsonAsync<List<MachineDowntimeItemDto>>(url, Ct);

        Assert.NotNull(items);
        var wb02 = items.First(i => i.MachineCode == "WB-02");

        // 10 minutes Running = 600s
        Assert.Equal(600, wb02.RunningSeconds);
        // 5 minutes Idle = 300s
        Assert.Equal(300, wb02.IdleSeconds);
        // 15 minutes Down (from t0+15 to t0+30 clamp) = 900s
        Assert.Equal(900, wb02.DownSeconds);
        Assert.Equal(1800, wb02.TotalTrackedSeconds);

        // Availability = 600 / 1800 = 33.33%
        Assert.Equal(33.33, wb02.AvailabilityPercent);
    }
}