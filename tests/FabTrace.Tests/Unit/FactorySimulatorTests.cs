using System.Text.Json;

using FabTrace.Contracts;
using FabTrace.Core.Demo;
using FabTrace.Generator.Simulation;

namespace FabTrace.Tests.Unit;

/// <summary>The default four-week history, simulated once and shared by the tests below.</summary>
public sealed class SimulatedHistory
{
    public ScenarioOptions Options { get; } = new()
    {
        Start = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.FromHours(8)),
    };

    public IReadOnlyList<SimEvent> Events { get; }
    public IReadOnlyList<ResultEvent> Results { get; }

    public SimulatedHistory()
    {
        Events = new FactorySimulator(Options).Run().ToList();
        Results = Events.OfType<ResultEvent>().ToList();
    }

    public double FailRate(Func<ResultEvent, bool> filter)
    {
        var selected = Results.Where(filter).ToList();
        Assert.True(selected.Count >= 200, $"Only {selected.Count} results match; too few to judge a rate.");
        return selected.Count(e => e.Result.Verdict == Verdict.Fail) / (double)selected.Count;
    }
}

public class FactorySimulatorTests(SimulatedHistory history) : IClassFixture<SimulatedHistory>
{
    private ScenarioOptions Options => history.Options;

    [Fact]
    public void Same_seed_gives_the_same_history()
    {
        var options = Options with { Weeks = 1 };

        var first = JsonSerializer.Serialize<IEnumerable<object>>(new FactorySimulator(options).Run());
        var second = JsonSerializer.Serialize<IEnumerable<object>>(new FactorySimulator(options).Run());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_seed_gives_different_readings_but_the_same_ids()
    {
        var first = new FactorySimulator(Options with { Weeks = 1, Seed = 1 }).Run().OfType<ResultEvent>().First().Result;
        var second = new FactorySimulator(Options with { Weeks = 1, Seed = 2 }).Run().OfType<ResultEvent>().First().Result;

        Assert.NotEqual(first.Readings[0].Value, second.Readings[0].Value);
        Assert.Equal(first.ResultId, second.ResultId);
    }

    [Fact]
    public void Every_result_id_is_unique()
    {
        Assert.Equal(history.Results.Count, history.Results.Select(e => e.Result.ResultId).Distinct().Count());
    }

    [Fact]
    public void Bad_recipe_is_only_used_inside_its_window()
    {
        var badRecipeResults = history.Results.Where(IsBadRecipe).ToList();

        Assert.NotEmpty(badRecipeResults);
        // A lot that starts just before the window closes finishes a couple of hours after it.
        Assert.All(badRecipeResults, e => Assert.InRange(
            e.Result.InspectedAt, Options.BadRecipeFrom, Options.BadRecipeTo.AddHours(3)));
    }

    [Fact]
    public void Bad_recipe_fails_far_more_wire_bonds_than_the_good_one()
    {
        var bad = history.FailRate(IsBadRecipe);
        var good = history.FailRate(e => e.Result is { RecipeCode: ScenarioOptions.BadRecipeCode, RecipeVersion: 1 });
        var otherProductInWindow = history.FailRate(e =>
            e.Result.RecipeCode == "BOND-QFN48" &&
            e.Result.InspectedAt >= Options.BadRecipeFrom && e.Result.InspectedAt < Options.BadRecipeTo);

        Assert.InRange(bad, 0.10, 0.25);
        Assert.InRange(good, 0.0, 0.04);
        Assert.InRange(otherProductInWindow, 0.0, 0.04);
    }

    [Fact]
    public void Drifting_tester_fails_more_and_more_until_its_socket_is_replaced()
    {
        var replaced = SocketReplacedAt();
        static bool OnTester(ResultEvent e, string machine, DateTimeOffset from, DateTimeOffset to) =>
            e.MachineCode == machine && e.Result.InspectedAt >= from && e.Result.InspectedAt < to;

        var beforeDrift = history.FailRate(e => OnTester(e, ScenarioOptions.DriftMachineCode, Options.Start, Options.DriftFrom));
        var midDrift = history.FailRate(e => OnTester(e, ScenarioOptions.DriftMachineCode, Options.DriftFrom.AddDays(3), Options.DriftFrom.AddDays(4)));
        var endOfDrift = history.FailRate(e => OnTester(e, ScenarioOptions.DriftMachineCode, Options.DriftTo.AddDays(-1), Options.DriftTo));
        var afterRepair = history.FailRate(e => OnTester(e, ScenarioOptions.DriftMachineCode, replaced, Options.End));
        var healthyTester = history.FailRate(e => OnTester(e, "FT-01", Options.DriftTo.AddDays(-1), Options.DriftTo));

        Assert.InRange(beforeDrift, 0.0, 0.05);
        Assert.True(midDrift > beforeDrift * 2, $"Mid-drift {midDrift:P1} should be well above the baseline {beforeDrift:P1}.");
        Assert.True(endOfDrift > 0.20, $"End of drift {endOfDrift:P1} should be above 20%.");
        Assert.True(endOfDrift > midDrift, "The fail rate should keep rising during the drift.");
        Assert.InRange(afterRepair, 0.0, 0.05);
        Assert.InRange(healthyTester, 0.0, 0.05);
    }

    [Fact]
    public void Drift_ends_with_an_alarm_downtime_and_a_socket_replacement()
    {
        var alarm = history.Events.OfType<AlarmRaisedEvent>()
            .Single(e => e.Alarm.AlarmCode == ScenarioOptions.DriftAlarmCode);
        var states = history.Events.OfType<StateEvent>()
            .Where(e => e.MachineCode == ScenarioOptions.DriftMachineCode && e.State.StartedAt >= alarm.Alarm.RaisedAt)
            .Take(3)
            .Select(e => (e.State.State, e.State.ReasonCode))
            .ToList();

        Assert.Equal(ScenarioOptions.DriftMachineCode, alarm.MachineCode);
        Assert.Equal(AlarmSeverity.Critical, alarm.Alarm.Severity);
        Assert.Equal(
            [(MachineState.Down, ScenarioOptions.DriftAlarmCode), (MachineState.Maintenance, "SOCKET_REPLACE"), (MachineState.Setup, "LOT_CHANGE")],
            states);
    }

    [Fact]
    public void A_unit_that_fails_a_step_never_reaches_the_next_one()
    {
        var failedAt = history.Results
            .Where(e => e.Result.Verdict == Verdict.Fail)
            .ToDictionary(e => e.Result.SerialNumber, e => StepSequence(e.Result.ProcessStepCode));

        Assert.DoesNotContain(history.Results, e =>
            failedAt.TryGetValue(e.Result.SerialNumber, out var failedStep) &&
            StepSequence(e.Result.ProcessStepCode) > failedStep);
    }

    [Fact]
    public void Each_machine_reports_in_time_order_and_never_repeats_a_state()
    {
        foreach (var machine in DemoFactory.Machines)
        {
            var resultTimes = history.Results.Where(e => e.MachineCode == machine.Code).Select(e => e.Result.InspectedAt).ToList();
            var states = history.Events.OfType<StateEvent>().Where(e => e.MachineCode == machine.Code).Select(e => e.State).ToList();

            Assert.NotEmpty(resultTimes);
            Assert.True(resultTimes.Zip(resultTimes.Skip(1)).All(pair => pair.First < pair.Second), $"{machine.Code} results out of order.");
            Assert.True(states.Zip(states.Skip(1)).All(pair => pair.First.StartedAt <= pair.Second.StartedAt), $"{machine.Code} states out of order.");
            Assert.True(states.Zip(states.Skip(1)).All(pair => pair.First.State != pair.Second.State), $"{machine.Code} repeats a state.");
        }
    }

    [Fact]
    public void Every_alarm_is_cleared_after_it_was_raised()
    {
        var raised = history.Events.OfType<AlarmRaisedEvent>().ToDictionary(e => e.Alarm.EventId, e => e.Alarm.RaisedAt);
        var cleared = history.Events.OfType<AlarmClearedEvent>().ToList();

        Assert.NotEmpty(raised);
        Assert.Equal(raised.Count, cleared.Count);
        Assert.All(cleared, e => Assert.True(e.ClearedAt > raised[e.EventId]));
    }

    [Fact]
    public void Operator_on_a_result_works_the_shift_it_was_inspected_in()
    {
        var operators = DemoFactory.Operators.ToDictionary(o => o.BadgeNo);

        Assert.All(history.Results.Take(5000), e =>
            Assert.Equal(DemoFactory.ShiftAt(e.Result.InspectedAt).Name, operators[e.Result.OperatorBadge].ShiftName));
    }

    private static bool IsBadRecipe(ResultEvent e) =>
        e.Result is { RecipeCode: ScenarioOptions.BadRecipeCode, RecipeVersion: ScenarioOptions.BadRecipeVersion };

    private static int StepSequence(string stepCode) => DemoFactory.Steps.Single(s => s.Code == stepCode).Sequence;

    private DateTimeOffset SocketReplacedAt()
    {
        var alarmId = history.Events.OfType<AlarmRaisedEvent>()
            .Single(e => e.Alarm.AlarmCode == ScenarioOptions.DriftAlarmCode).Alarm.EventId;
        return history.Events.OfType<AlarmClearedEvent>().Single(e => e.EventId == alarmId).ClearedAt;
    }
}