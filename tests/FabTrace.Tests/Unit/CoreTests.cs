using FabTrace.Core.Demo;
using FabTrace.Core.Entities;
using FabTrace.Core.Security;
using FabTrace.Generator;

namespace FabTrace.Tests.Unit;

public class ApiKeyHasherTests
{
    [Fact]
    public void Hash_is_32_bytes_and_repeatable()
    {
        var first = ApiKeyHasher.Hash("dev-key-WB-01");
        var second = ApiKeyHasher.Hash("dev-key-WB-01");

        Assert.Equal(ApiKeyHasher.HashSizeBytes, first.Length);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Hash_matches_the_published_sha256_test_vector()
    {
        Assert.Equal(
            "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
            Convert.ToHexString(ApiKeyHasher.Hash("abc")));
    }

    [Fact]
    public void Different_keys_give_different_hashes()
    {
        Assert.NotEqual(ApiKeyHasher.Hash("dev-key-WB-01"), ApiKeyHasher.Hash("dev-key-WB-02"));
    }
}

public class ShiftTests
{
    private static readonly Shift Night = new() { Name = "Night", StartTime = new TimeOnly(23, 0), EndTime = new TimeOnly(7, 0) };

    [Theory]
    [InlineData(23, 0, true)]   // start is included
    [InlineData(2, 30, true)]   // after midnight
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]   // end is excluded: 07:00 belongs to the next shift
    [InlineData(15, 0, false)]
    public void Shift_that_crosses_midnight_covers_the_right_times(int hour, int minute, bool expected)
    {
        Assert.Equal(expected, Night.Covers(new TimeOnly(hour, minute)));
    }

    [Theory]
    [InlineData("2026-03-01T23:00:00Z", "Morning")]   // 07:00 in Malaysia
    [InlineData("2026-03-02T06:59:59Z", "Morning")]   // 14:59
    [InlineData("2026-03-02T07:00:00Z", "Afternoon")] // 15:00
    [InlineData("2026-03-02T15:00:00Z", "Night")]     // 23:00
    [InlineData("2026-03-02T22:59:59Z", "Night")]     // 06:59 next day
    public void Shift_is_decided_in_factory_local_time(string utc, string expectedShift)
    {
        Assert.Equal(expectedShift, DemoFactory.ShiftAt(DateTimeOffset.Parse(utc)).Name);
    }

    [Fact]
    public void Every_shift_has_an_operator_for_every_machine_type()
    {
        foreach (var shift in DemoFactory.Shifts)
        {
            foreach (var machineType in DemoFactory.Machines.Select(m => m.MachineType).Distinct())
            {
                Assert.Single(DemoFactory.Operators, o => o.ShiftName == shift.Name && o.MachineType == machineType);
            }
        }
    }
}

public class GeneratorOptionsTests
{
    [Fact]
    public void No_arguments_gives_the_defaults()
    {
        Assert.Equal(new GeneratorOptions(), GeneratorOptions.Parse([]));
    }

    [Fact]
    public void Options_are_read_by_name_without_regard_to_case()
    {
        var options = GeneratorOptions.Parse(["--weeks", "2", "--Seed", "7", "--ApiUrl", "http://server:9000", "--Start", "2026-02-02"]);

        Assert.Equal((2, 7, "http://server:9000", new DateOnly(2026, 2, 2)), (options.Weeks, options.Seed, options.ApiUrl, options.Start));
    }

    [Theory]
    [InlineData("--Nope", "1")]
    [InlineData("--Weeks", "0")]
    [InlineData("--Weeks", "many")]
    [InlineData("--BatchSize", "5000")]
    [InlineData("--Weeks")]
    public void Invalid_arguments_are_refused(params string[] args)
    {
        Assert.Throws<ArgumentException>(() => GeneratorOptions.Parse(args));
    }

    [Fact]
    public void Default_history_ends_at_the_start_of_yesterday_in_factory_time()
    {
        var now = DateTimeOffset.Parse("2026-10-13T02:00:00Z"); // 10:00 on 13 October in Malaysia

        var scenario = new GeneratorOptions().ToScenario(now);

        Assert.Equal(DateTimeOffset.Parse("2026-10-12T00:00:00+08:00"), scenario.End);
        Assert.Equal(DateTimeOffset.Parse("2026-09-14T00:00:00+08:00"), scenario.Start);
    }
}