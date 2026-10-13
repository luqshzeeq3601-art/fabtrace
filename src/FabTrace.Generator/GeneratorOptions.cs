using System.Globalization;

using FabTrace.Contracts;
using FabTrace.Core;
using FabTrace.Generator.Simulation;

namespace FabTrace.Generator;

/// <summary>Command-line options, written as <c>--Name value</c> pairs.</summary>
public sealed record GeneratorOptions
{
    public const string Usage = """
        Usage: dotnet run --project src/FabTrace.Generator -- [options]

          --ApiUrl <url>       FabTrace API address          (default http://localhost:5080)
          --Weeks <n>          Weeks of history to replay    (default 4; both planted problems need 4)
          --Seed <n>           Random seed                   (default 42)
          --UnitsPerLot <n>    Units in each lot             (default 100)
          --BatchSize <n>      Items per request, max 1000   (default 500)
          --Start <yyyy-MM-dd> First day, factory local time (default: so the history ends yesterday)
        """;

    public string ApiUrl { get; init; } = "http://localhost:5080";
    public int Weeks { get; init; } = 4;
    public int Seed { get; init; } = 42;
    public int UnitsPerLot { get; init; } = 100;
    public int BatchSize { get; init; } = 500;
    public DateOnly? Start { get; init; }

    public static GeneratorOptions Parse(string[] args)
    {
        if (args.Length % 2 != 0)
        {
            throw new ArgumentException("Every option needs a value.");
        }

        var options = new GeneratorOptions();
        for (var index = 0; index < args.Length; index += 2)
        {
            var value = args[index + 1];
            options = args[index].ToLowerInvariant() switch
            {
                "--apiurl" => options with { ApiUrl = value },
                "--weeks" => options with { Weeks = PositiveInt(value, "Weeks") },
                "--seed" => options with { Seed = int.Parse(value, CultureInfo.InvariantCulture) },
                "--unitsperlot" => options with { UnitsPerLot = PositiveInt(value, "UnitsPerLot") },
                "--batchsize" => options with { BatchSize = PositiveInt(value, "BatchSize") },
                "--start" => options with { Start = DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture) },
                _ => throw new ArgumentException($"Unknown option '{args[index]}'."),
            };
        }

        if (options.BatchSize > ApiConventions.MaxBatchSize)
        {
            throw new ArgumentException($"BatchSize cannot be more than {ApiConventions.MaxBatchSize}.");
        }

        return options;
    }

    public ScenarioOptions ToScenario(DateTimeOffset now)
    {
        // Default: finish at the start of yesterday (factory time), so even the
        // last lots, which run a little past the end, are in the past.
        var today = DateOnly.FromDateTime(FactoryTime.ToLocal(now).Date);
        var startDate = Start ?? today.AddDays(-(7 * Weeks) - 1);

        return new ScenarioOptions
        {
            Start = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), FactoryTime.UtcOffset),
            Weeks = Weeks,
            Seed = Seed,
            UnitsPerLot = UnitsPerLot,
        };
    }

    private static int PositiveInt(string value, string name) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number
            : throw new ArgumentException($"{name} must be a positive whole number.");
}