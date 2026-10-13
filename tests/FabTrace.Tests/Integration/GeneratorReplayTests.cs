using FabTrace.Generator.Publishing;
using FabTrace.Generator.Simulation;

using Microsoft.EntityFrameworkCore;

namespace FabTrace.Tests.Integration;

/// <summary>The generator and the API working together, as in a real replay.</summary>
[Collection(DatabaseCollection.Name)]
public class GeneratorReplayTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Replaying_the_same_history_twice_stores_it_once()
    {
        var scenario = new ScenarioOptions
        {
            Start = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.FromHours(8)),
            Weeks = 1,
            UnitsPerLot = 5,
        };
        var events = new FactorySimulator(scenario).Run().Take(3000).ToList();
        var results = events.OfType<ResultEvent>().Count();
        var states = events.OfType<StateEvent>().Count();
        var alarms = events.OfType<AlarmRaisedEvent>().Count();
        var clears = events.OfType<AlarmClearedEvent>().Count();
        Assert.True(results > 0 && states > 0 && alarms > 0 && clears > 0, "The sample should contain every kind of event.");

        using var client = Factory.CreateClient();
        var first = new ApiPublisher(client, batchSize: 200);
        await first.PublishAsync(events, Ct);
        var second = new ApiPublisher(client, batchSize: 200);
        await second.PublishAsync(events, Ct);

        Assert.Equal((results + states + alarms, 0, 0), (first.Created, first.Duplicates, first.Rejected));
        Assert.Equal((0, results + states + alarms, 0), (second.Created, second.Duplicates, second.Rejected));

        await using var db = Factory.CreateDbContext();
        Assert.Equal(results, await db.InspectionResults.CountAsync(Ct));
        Assert.Equal(states, await db.MachineStateEvents.CountAsync(Ct));
        Assert.Equal(alarms, await db.Alarms.CountAsync(Ct));
        Assert.Equal(clears, await db.Alarms.CountAsync(a => a.ClearedAt != null, Ct));
    }
}