using FabTrace.Contracts;
using FabTrace.Core.Demo;

namespace FabTrace.Generator.Simulation;

/// <summary>
/// Simulates the demo plant: two lines, each a wire bonder, a tester and a vision
/// inspector. Lots move down a line one step at a time; a unit that fails a step
/// is scrapped and does not reach the next one.
/// </summary>
/// <remarks>
/// Pure and deterministic: no clock, no I/O, and all randomness comes from one
/// seeded generator, so the same options always produce the same history.
/// Each machine's events come out in time order.
/// </remarks>
public sealed class FactorySimulator(ScenarioOptions options)
{
    private static readonly TimeSpan TransferTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromDays(7);
    private static readonly TimeSpan MaintenanceDuration = TimeSpan.FromHours(2);
    private const double StoppageChancePerLot = 0.06;
    private const double Qfn32Share = 0.6;

    private readonly Random _random = new(options.Seed);
    private bool _socketReplaced;

    public IEnumerable<SimEvent> Run()
    {
        var lines = DemoFactory.Machines
            .Select((demo, index) => new MachineSim(demo, options.Start, firstMaintenanceAt: options.Start.AddDays(2 + index)))
            .GroupBy(machine => machine.Demo.Line)
            .Select(line => line.ToList())
            .ToList();

        // Always give the next lot to the line whose bonder is free first, so lot
        // numbers rise with time across the whole plant.
        for (var lotSequence = 1; ; lotSequence++)
        {
            var line = lines.MinBy(machines => machines[0].FreeAt)!;
            if (line[0].FreeAt >= options.End)
            {
                break;
            }

            foreach (var simEvent in RunLot(line, lotSequence))
            {
                yield return simEvent;
            }
        }

        foreach (var machine in lines.SelectMany(machines => machines))
        {
            var events = new List<SimEvent>();
            SetState(machine, MachineState.Idle, machine.FreeAt, reason: null, events);
            foreach (var simEvent in events)
            {
                yield return simEvent;
            }
        }
    }

    private List<SimEvent> RunLot(List<MachineSim> line, int lotSequence)
    {
        var events = new List<SimEvent>();
        var lotNumber = $"LOT-{lotSequence:D4}";
        var productCode = _random.NextDouble() < Qfn32Share ? DemoFactory.Qfn32 : DemoFactory.Qfn48;
        var units = Enumerable.Range(1, options.UnitsPerLot).Select(n => $"{lotNumber}-{n:D3}").ToList();
        var readyAt = line[0].FreeAt;

        foreach (var machine in line)
        {
            units = RunStep(machine, lotNumber, productCode, units, readyAt, events);
            readyAt = machine.FreeAt + TransferTime;
            if (units.Count == 0)
            {
                break;
            }
        }

        return events;
    }

    /// <summary>Processes the lot's units on one machine and returns the ones that passed.</summary>
    private List<string> RunStep(
        MachineSim machine, string lotNumber, string productCode, List<string> units, DateTimeOffset readyAt, List<SimEvent> events)
    {
        var clock = readyAt > machine.FreeAt ? readyAt : machine.FreeAt;
        if (clock > machine.FreeAt)
        {
            SetState(machine, MachineState.Idle, machine.FreeAt, reason: null, events);
        }

        clock = ReplaceWornSocketIfDue(machine, clock, events);
        clock = WeeklyMaintenanceIfDue(machine, clock, events);

        SetState(machine, MachineState.Setup, clock, "LOT_CHANGE", events);
        clock += machine.SetupTime;
        SetState(machine, MachineState.Running, clock, reason: null, events);

        var recipe = RecipeInUse(productCode, machine.Demo.StepCode, clock);
        int? stopBeforeUnit = _random.NextDouble() < StoppageChancePerLot ? _random.Next(1, units.Count) : null;
        var passed = new List<string>();

        for (var index = 0; index < units.Count; index++)
        {
            if (index == stopBeforeUnit)
            {
                clock = Stoppage(machine, clock, events);
            }

            var cycleTime = machine.CycleTime * (0.9 + 0.3 * _random.NextDouble());
            clock += cycleTime;

            var result = Inspect(machine, lotNumber, units[index], recipe, clock, cycleTime);
            events.Add(new ResultEvent(machine.Demo.Code, result));
            if (result.Verdict == Verdict.Pass)
            {
                passed.Add(units[index]);
            }
        }

        machine.FreeAt = clock;
        return passed;
    }

    private DemoFactory.DemoRecipe RecipeInUse(string productCode, string stepCode, DateTimeOffset at)
    {
        var badRecipeActive = at >= options.BadRecipeFrom && at < options.BadRecipeTo;
        var current = DemoFactory.RecipeFor(productCode, stepCode);
        return badRecipeActive && current.Code == ScenarioOptions.BadRecipeCode
            ? DemoFactory.RecipeFor(productCode, stepCode, ScenarioOptions.BadRecipeVersion)
            : current;
    }

    private InspectionResultDto Inspect(
        MachineSim machine, string lotNumber, string serialNumber, DemoFactory.DemoRecipe recipe, DateTimeOffset at, TimeSpan cycleTime)
    {
        var readings = new List<TestReadingDto>();
        string?[] defects = machine.Demo.StepCode switch
        {
            DemoFactory.WireBond =>
            [
                Measure(readings, "PullStrength", IsBadRecipe(recipe) ? 6.5 : 8.0, 1.45, lower: 5.0, upper: null, "gf", "WIRE_LIFT"),
                Measure(readings, "BallShear", 32.0, 4.0, lower: 20.0, upper: null, "gf", "BALL_SHEAR_LOW"),
            ],
            DemoFactory.FinalTest =>
            [
                Measure(readings, "ContactResistance", 35.0 + SocketWear(machine, at), 6.0, lower: null, upper: 50.0, "mOhm", "CONTACT_RES_HIGH"),
                Measure(readings, "LeakageCurrent", 0.50, 0.20, lower: null, upper: 1.0, "uA", "LEAKAGE_HIGH"),
                Measure(readings, "OutputVoltage", 3.30, 0.04, lower: 3.20, upper: 3.40, "V", "VOUT_OUT_OF_SPEC"),
            ],
            _ =>
            [
                Measure(readings, "Coplanarity", 45.0, 12.0, lower: null, upper: 80.0, "um", "COPLANARITY_HIGH"),
                Measure(readings, "MarkContrast", 0.82, 0.07, lower: 0.60, upper: null, "ratio", "MARK_ILLEGIBLE"),
                CosmeticDefect(),
            ],
        };

        var defectCode = defects.FirstOrDefault(defect => defect is not null);
        return new InspectionResultDto
        {
            ResultId = DeterministicGuid.Create($"result|{serialNumber}|{machine.Demo.StepCode}"),
            LotNumber = lotNumber,
            SerialNumber = serialNumber,
            ProcessStepCode = machine.Demo.StepCode,
            RecipeCode = recipe.Code,
            RecipeVersion = recipe.Version,
            OperatorBadge = DemoFactory.OperatorFor(machine.Demo.MachineType, at).BadgeNo,
            Verdict = defectCode is null ? Verdict.Pass : Verdict.Fail,
            DefectCode = defectCode,
            InspectedAt = at,
            CycleTimeMs = (int)cycleTime.TotalMilliseconds,
            Readings = readings,
        };
    }

    private static bool IsBadRecipe(DemoFactory.DemoRecipe recipe) =>
        recipe is { Code: ScenarioOptions.BadRecipeCode, Version: ScenarioOptions.BadRecipeVersion };

    /// <summary>Extra contact resistance on the drifting tester: 0 at the start of the drift, 12 mOhm at the end.</summary>
    private double SocketWear(MachineSim machine, DateTimeOffset at)
    {
        if (machine.Demo.Code != ScenarioOptions.DriftMachineCode || _socketReplaced || at < options.DriftFrom)
        {
            return 0;
        }

        return 12.0 * ((at - options.DriftFrom) / (options.DriftTo - options.DriftFrom));
    }

    /// <summary>Records one reading and returns the defect code when it is outside its limits.</summary>
    private string? Measure(
        List<TestReadingDto> readings, string name, double mean, double sigma, double? lower, double? upper, string unit, string defectCode)
    {
        var value = Math.Round(NextGaussian(mean, sigma), 3);
        readings.Add(new TestReadingDto { ParameterName = name, Value = value, LowerLimit = lower, UpperLimit = upper, Unit = unit });
        return value < lower || value > upper ? defectCode : null;
    }

    /// <summary>Visual defects that no measurement predicts: about 1.5% of units.</summary>
    private string? CosmeticDefect() =>
        _random.NextDouble() < 0.015 ? Pick("SCRATCH", "LEAD_BENT", "FOREIGN_MATERIAL") : null;

    /// <summary>A random unplanned stop in the middle of a lot: alarm, 5 to 25 minutes down, alarm cleared.</summary>
    private DateTimeOffset Stoppage(MachineSim machine, DateTimeOffset clock, List<SimEvent> events)
    {
        var (code, severity, message) = machine.Demo.MachineType switch
        {
            DemoFactory.WireBonder => Pick(
                ("WIRE_BREAK", AlarmSeverity.Warning, "Wire broken at bond head"),
                ("BOND_HEAD_OVERTEMP", AlarmSeverity.Critical, "Bond head temperature above limit")),
            DemoFactory.Tester => Pick(
                ("HANDLER_JAM", AlarmSeverity.Warning, "Unit jammed in handler track"),
                ("TEST_HEAD_TIMEOUT", AlarmSeverity.Critical, "Test head did not respond")),
            _ => Pick(
                ("CAMERA_TIMEOUT", AlarmSeverity.Warning, "Camera did not return an image"),
                ("LIGHTING_FAULT", AlarmSeverity.Critical, "Ring light current out of range")),
        };

        var alarmId = RaiseAlarm(machine, code, severity, message, clock, events);
        SetState(machine, MachineState.Down, clock, code, events);
        clock += TimeSpan.FromMinutes(5 + 20 * _random.NextDouble());
        events.Add(new AlarmClearedEvent(machine.Demo.Code, alarmId, clock));
        SetState(machine, MachineState.Running, clock, reason: null, events);
        return clock;
    }

    /// <summary>The end of the planted drift: the tester alarms, goes down and gets a new socket.</summary>
    private DateTimeOffset ReplaceWornSocketIfDue(MachineSim machine, DateTimeOffset clock, List<SimEvent> events)
    {
        if (machine.Demo.Code != ScenarioOptions.DriftMachineCode || _socketReplaced || clock < options.DriftTo)
        {
            return clock;
        }

        var alarmId = RaiseAlarm(
            machine, ScenarioOptions.DriftAlarmCode, AlarmSeverity.Critical, "Consecutive contact resistance failures", clock, events);
        SetState(machine, MachineState.Down, clock, ScenarioOptions.DriftAlarmCode, events);
        clock += TimeSpan.FromMinutes(20);
        SetState(machine, MachineState.Maintenance, clock, "SOCKET_REPLACE", events);
        clock += TimeSpan.FromMinutes(90);
        events.Add(new AlarmClearedEvent(machine.Demo.Code, alarmId, clock));
        _socketReplaced = true;
        return clock;
    }

    private static DateTimeOffset WeeklyMaintenanceIfDue(MachineSim machine, DateTimeOffset clock, List<SimEvent> events)
    {
        if (clock < machine.NextMaintenanceAt)
        {
            return clock;
        }

        SetState(machine, MachineState.Maintenance, clock, "PM_WEEKLY", events);
        machine.NextMaintenanceAt += MaintenanceInterval;
        return clock + MaintenanceDuration;
    }

    private static Guid RaiseAlarm(
        MachineSim machine, string code, AlarmSeverity severity, string message, DateTimeOffset at, List<SimEvent> events)
    {
        var eventId = DeterministicGuid.Create($"alarm|{machine.Demo.Code}|{++machine.AlarmCount}");
        events.Add(new AlarmRaisedEvent(machine.Demo.Code, new AlarmDto
        {
            EventId = eventId,
            AlarmCode = code,
            Severity = severity,
            Message = message,
            RaisedAt = at,
        }));
        return eventId;
    }

    /// <summary>Emits a state event, but only when the state actually changes.</summary>
    private static void SetState(MachineSim machine, MachineState state, DateTimeOffset at, string? reason, List<SimEvent> events)
    {
        if (machine.State == state)
        {
            return;
        }

        machine.State = state;
        events.Add(new StateEvent(machine.Demo.Code, new MachineStateEventDto
        {
            EventId = DeterministicGuid.Create($"state|{machine.Demo.Code}|{++machine.StateCount}"),
            State = state,
            StartedAt = at,
            ReasonCode = reason,
        }));
    }

    private T Pick<T>(params T[] choices) => choices[_random.Next(choices.Length)];

    /// <summary>A normally distributed random number (Box-Muller transform).</summary>
    private double NextGaussian(double mean, double sigma)
    {
        var u1 = 1.0 - _random.NextDouble();
        var u2 = _random.NextDouble();
        return mean + sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>The simulator's moving picture of one machine.</summary>
    private sealed class MachineSim(DemoFactory.DemoMachine demo, DateTimeOffset start, DateTimeOffset firstMaintenanceAt)
    {
        public DemoFactory.DemoMachine Demo { get; } = demo;
        public DateTimeOffset FreeAt { get; set; } = start;
        public DateTimeOffset NextMaintenanceAt { get; set; } = firstMaintenanceAt;
        public MachineState? State { get; set; }
        public int StateCount { get; set; }
        public int AlarmCount { get; set; }

        public TimeSpan CycleTime { get; } = TimeSpan.FromSeconds(demo.MachineType switch
        {
            DemoFactory.WireBonder => 55,
            DemoFactory.Tester => 30,
            _ => 20,
        });

        public TimeSpan SetupTime { get; } = TimeSpan.FromMinutes(demo.MachineType switch
        {
            DemoFactory.WireBonder => 12,
            DemoFactory.Tester => 6,
            _ => 4,
        });
    }
}