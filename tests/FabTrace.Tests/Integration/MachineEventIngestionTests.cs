using System.Net;
using System.Net.Http.Json;

using FabTrace.Contracts;

using Microsoft.EntityFrameworkCore;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class MachineEventIngestionTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Sending_the_same_alarm_twice_stores_it_once()
    {
        using var client = Factory.CreateMachineClient("WB-01");
        var alarm = TestData.Alarm();

        var first = await PostBatchAsync(client, ApiConventions.AlarmsRoute, alarm);
        var second = await PostBatchAsync(client, ApiConventions.AlarmsRoute, alarm);

        Assert.Equal(IngestStatus.Created, first.Items[0].Status);
        Assert.Equal(IngestStatus.Duplicate, second.Items[0].Status);

        await using var db = Factory.CreateDbContext();
        var stored = await db.Alarms.Include(a => a.Machine).SingleAsync(Ct);
        Assert.Equal("WB-01", stored.Machine!.Code);
        Assert.Equal(AlarmSeverity.Warning, stored.Severity);
        Assert.Null(stored.ClearedAt);
    }

    [Fact]
    public async Task Clearing_an_alarm_sets_the_clear_time_and_repeating_it_changes_nothing()
    {
        using var client = Factory.CreateMachineClient("WB-01");
        var alarm = TestData.Alarm();
        await PostBatchAsync(client, ApiConventions.AlarmsRoute, alarm);
        var clearedAt = alarm.RaisedAt.AddMinutes(12);

        var first = await ClearAsync(client, alarm.EventId, clearedAt);
        var second = await ClearAsync(client, alarm.EventId, clearedAt.AddMinutes(30));

        Assert.Equal(HttpStatusCode.NoContent, first);
        Assert.Equal(HttpStatusCode.NoContent, second);
        await using var db = Factory.CreateDbContext();
        Assert.Equal(clearedAt.UtcDateTime, (await db.Alarms.SingleAsync(Ct)).ClearedAt);
    }

    [Fact]
    public async Task Clearing_an_unknown_alarm_is_not_found()
    {
        using var client = Factory.CreateMachineClient("WB-01");

        Assert.Equal(HttpStatusCode.NotFound, await ClearAsync(client, Guid.NewGuid(), TestData.BaseTime));
    }

    [Fact]
    public async Task A_machine_cannot_clear_another_machines_alarm()
    {
        using var owner = Factory.CreateMachineClient("WB-01");
        using var other = Factory.CreateMachineClient("WB-02");
        var alarm = TestData.Alarm();
        await PostBatchAsync(owner, ApiConventions.AlarmsRoute, alarm);

        var status = await ClearAsync(other, alarm.EventId, alarm.RaisedAt.AddMinutes(5));

        Assert.Equal(HttpStatusCode.NotFound, status);
        await using var db = Factory.CreateDbContext();
        Assert.Null((await db.Alarms.SingleAsync(Ct)).ClearedAt);
    }

    [Fact]
    public async Task Clear_time_before_raise_time_is_a_bad_request()
    {
        using var client = Factory.CreateMachineClient("WB-01");
        var alarm = TestData.Alarm();
        await PostBatchAsync(client, ApiConventions.AlarmsRoute, alarm);

        var status = await ClearAsync(client, alarm.EventId, alarm.RaisedAt.AddMinutes(-1));

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Sending_the_same_state_events_twice_stores_them_once()
    {
        using var client = Factory.CreateMachineClient("FT-01");
        MachineStateEventDto[] events =
        [
            TestData.State(MachineState.Setup, 0, "LOT_CHANGE"),
            TestData.State(MachineState.Running, 6),
            TestData.State(MachineState.Down, 40, "HANDLER_JAM"),
        ];

        var first = await PostBatchAsync(client, ApiConventions.MachineStatesRoute, events);
        var second = await PostBatchAsync(client, ApiConventions.MachineStatesRoute, events);

        Assert.Equal(3, first.Created);
        Assert.Equal(3, second.Duplicates);

        await using var db = Factory.CreateDbContext();
        var stored = await db.MachineStateEvents.OrderBy(e => e.StartedAt).ToListAsync(Ct);
        Assert.Equal([MachineState.Setup, MachineState.Running, MachineState.Down], stored.Select(e => e.State));
        Assert.Equal("HANDLER_JAM", stored[2].ReasonCode);
    }

    private static async Task<HttpStatusCode> ClearAsync(HttpClient client, Guid eventId, DateTimeOffset clearedAt)
    {
        var request = new ClearAlarmRequest { ClearedAt = clearedAt };
        using var response = await client.PostAsJsonAsync(ApiConventions.ClearAlarmRoute(eventId), request, Ct);
        return response.StatusCode;
    }
}