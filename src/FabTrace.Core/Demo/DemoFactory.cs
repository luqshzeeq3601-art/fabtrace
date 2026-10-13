namespace FabTrace.Core.Demo;

/// <summary>
/// The master data of the demo plant: a small semiconductor assembly and test
/// floor with two lines. The database seeder and the data generator both read
/// this, so the codes the generator sends always exist on the server.
/// </summary>
public static class DemoFactory
{
    public const string WireBond = "WIRE_BOND";
    public const string FinalTest = "FINAL_TEST";
    public const string VisionInspect = "VISION_INSPECT";

    public const string WireBonder = "WireBonder";
    public const string Tester = "Tester";
    public const string VisionInspection = "VisionInspection";

    public const string Qfn32 = "QFN32";
    public const string Qfn48 = "QFN48";

    public sealed record DemoShift(string Name, TimeOnly Start, TimeOnly End);
    public sealed record DemoStep(string Code, string Name, int Sequence);
    public sealed record DemoMachine(string Code, string Name, string MachineType, string Line, string StepCode);
    public sealed record DemoOperator(string BadgeNo, string Name, string ShiftName, string MachineType);
    public sealed record DemoParameter(string Name, double Target, double? Lower, double? Upper, string Unit);
    public sealed record DemoRecipe(string Code, int Version, string ProductCode, string StepCode, DemoParameter[] Parameters);

    public static readonly DemoShift[] Shifts =
    [
        new("Morning", new TimeOnly(7, 0), new TimeOnly(15, 0)),
        new("Afternoon", new TimeOnly(15, 0), new TimeOnly(23, 0)),
        new("Night", new TimeOnly(23, 0), new TimeOnly(7, 0)),
    ];

    public static readonly DemoStep[] Steps =
    [
        new(WireBond, "Wire bond", 1),
        new(FinalTest, "Final test", 2),
        new(VisionInspect, "Vision inspection", 3),
    ];

    public static readonly DemoMachine[] Machines =
    [
        new("WB-01", "Wire bonder 1", WireBonder, "LINE-1", WireBond),
        new("FT-01", "Final tester 1", Tester, "LINE-1", FinalTest),
        new("VI-01", "Vision inspector 1", VisionInspection, "LINE-1", VisionInspect),
        new("WB-02", "Wire bonder 2", WireBonder, "LINE-2", WireBond),
        new("FT-02", "Final tester 2", Tester, "LINE-2", FinalTest),
        new("VI-02", "Vision inspector 2", VisionInspection, "LINE-2", VisionInspect),
    ];

    // One operator per shift looks after both machines of a type.
    public static readonly DemoOperator[] Operators =
    [
        new("OP101", "Aisyah Rahman", "Morning", WireBonder),
        new("OP102", "Tan Wei Ming", "Morning", Tester),
        new("OP103", "Kumar Subramaniam", "Morning", VisionInspection),
        new("OP201", "Nurul Huda", "Afternoon", WireBonder),
        new("OP202", "Lim Mei Ling", "Afternoon", Tester),
        new("OP203", "Ravi Chandran", "Afternoon", VisionInspection),
        new("OP301", "Hafiz Ismail", "Night", WireBonder),
        new("OP302", "Chong Kah Wai", "Night", Tester),
        new("OP303", "Siti Zainab", "Night", VisionInspection),
    ];

    public static readonly DemoRecipe[] Recipes =
    [
        new("BOND-QFN32", 1, Qfn32, WireBond,
        [
            new("BondForce", 30, 25, 35, "gf"),
            new("UltrasonicPower", 90, 80, 100, "mW"),
            new("BondTime", 15, 12, 18, "ms"),
        ]),
        // The planted "bad recipe": ultrasonic power was lowered, which weakens the bond.
        new("BOND-QFN32", 2, Qfn32, WireBond,
        [
            new("BondForce", 30, 25, 35, "gf"),
            new("UltrasonicPower", 70, 60, 80, "mW"),
            new("BondTime", 15, 12, 18, "ms"),
        ]),
        new("BOND-QFN48", 1, Qfn48, WireBond,
        [
            new("BondForce", 32, 27, 37, "gf"),
            new("UltrasonicPower", 95, 85, 105, "mW"),
            new("BondTime", 16, 13, 19, "ms"),
        ]),
        new("TEST-QFN32", 1, Qfn32, FinalTest,
        [
            new("SupplyVoltage", 3.3, 3.25, 3.35, "V"),
            new("TestTemperature", 25, 22, 28, "C"),
        ]),
        new("TEST-QFN48", 1, Qfn48, FinalTest,
        [
            new("SupplyVoltage", 3.3, 3.25, 3.35, "V"),
            new("TestTemperature", 25, 22, 28, "C"),
        ]),
        new("VIS-QFN32", 1, Qfn32, VisionInspect,
        [
            new("ExposureTime", 8, 6, 10, "ms"),
            new("LightIntensity", 70, 60, 80, "%"),
        ]),
        new("VIS-QFN48", 1, Qfn48, VisionInspect,
        [
            new("ExposureTime", 8, 6, 10, "ms"),
            new("LightIntensity", 75, 65, 85, "%"),
        ]),
    ];

    /// <summary>
    /// API keys for the demo machines. They are public by design and are only
    /// seeded in the Development environment; see Security.md.
    /// </summary>
    public static string DevApiKey(string machineCode) => $"dev-key-{machineCode}";

    public static DemoShift ShiftAt(DateTimeOffset instant)
    {
        var localTime = FactoryTime.LocalTimeOfDay(instant);
        return Shifts.First(shift => localTime.IsBetween(shift.Start, shift.End));
    }

    public static DemoOperator OperatorFor(string machineType, DateTimeOffset instant)
    {
        var shift = ShiftAt(instant);
        return Operators.First(o => o.ShiftName == shift.Name && o.MachineType == machineType);
    }

    public static DemoRecipe RecipeFor(string productCode, string stepCode, int version = 1) =>
        Recipes.First(r => r.ProductCode == productCode && r.StepCode == stepCode && r.Version == version);
}