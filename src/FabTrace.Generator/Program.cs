using System.Diagnostics;

using FabTrace.Generator;
using FabTrace.Generator.Publishing;
using FabTrace.Generator.Simulation;

GeneratorOptions options;
try
{
    options = GeneratorOptions.Parse(args);
}
catch (Exception exception) when (exception is ArgumentException or FormatException)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(GeneratorOptions.Usage);
    return 2;
}

using var http = new HttpClient { BaseAddress = new Uri(options.ApiUrl.TrimEnd('/') + "/") };
try
{
    using var health = await http.GetAsync("health");
    health.EnsureSuccessStatusCode();
}
catch (HttpRequestException)
{
    Console.Error.WriteLine($"Cannot reach the FabTrace API at {options.ApiUrl}.");
    Console.Error.WriteLine("Start it first:  dotnet run --project src/FabTrace.Api");
    return 1;
}

var scenario = options.ToScenario(DateTimeOffset.UtcNow);
Console.WriteLine($"Replaying {scenario.Weeks} week(s) from {scenario.Start:yyyy-MM-dd} to {scenario.End:yyyy-MM-dd} (seed {scenario.Seed}).");
Console.WriteLine($"  Bad recipe  {ScenarioOptions.BadRecipeCode} v{ScenarioOptions.BadRecipeVersion}: {scenario.BadRecipeFrom:yyyy-MM-dd} to {scenario.BadRecipeTo:yyyy-MM-dd}");
Console.WriteLine($"  Drift on    {ScenarioOptions.DriftMachineCode}: {scenario.DriftFrom:yyyy-MM-dd} to {scenario.DriftTo:yyyy-MM-dd}");

var stopwatch = Stopwatch.StartNew();
var publisher = new ApiPublisher(http, options.BatchSize, Console.WriteLine);
await publisher.PublishAsync(new FactorySimulator(scenario).Run());

Console.WriteLine($"Done in {stopwatch.Elapsed.TotalSeconds:N0} s.");
Console.WriteLine($"  Created {publisher.Created:N0}, duplicates {publisher.Duplicates:N0}, rejected {publisher.Rejected:N0}");
foreach (var rejection in publisher.RejectionSamples)
{
    Console.WriteLine($"  Rejected: {rejection}");
}

return publisher.Rejected == 0 ? 0 : 1;