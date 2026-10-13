namespace FabTrace.Generator.Simulation;

/// <summary>What to simulate, including when the two planted yield problems happen.</summary>
public sealed record ScenarioOptions
{
    public required DateTimeOffset Start { get; init; }
    public int Weeks { get; init; } = 4;
    public int Seed { get; init; } = 42;
    public int UnitsPerLot { get; init; } = 100;

    public DateTimeOffset End => Start.AddDays(7 * Weeks);

    // Planted problem 1: a bad wire-bond recipe version is in use for three days in week 3.
    public const string BadRecipeCode = "BOND-QFN32";
    public const int BadRecipeVersion = 2;
    public DateTimeOffset BadRecipeFrom => Start.AddDays(15);
    public DateTimeOffset BadRecipeTo => Start.AddDays(18);

    // Planted problem 2: one tester's contact resistance creeps up for a week (a
    // wearing test socket) until the machine alarms and the socket is replaced.
    public const string DriftMachineCode = "FT-02";
    public const string DriftAlarmCode = "SOCKET_CONTACT_FAIL";
    public DateTimeOffset DriftFrom => Start.AddDays(19);
    public DateTimeOffset DriftTo => Start.AddDays(26);
}