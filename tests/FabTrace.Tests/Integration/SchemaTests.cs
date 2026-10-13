using FabTrace.Core.Demo;
using FabTrace.Data.Seeding;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FabTrace.Tests.Integration;

/// <summary>
/// Checks the rules that live in SQL Server itself, by going around the
/// application with raw SQL. These guarantees hold for any client.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class SchemaTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Migrations_build_the_whole_schema_from_an_empty_database()
    {
        await using var db = Factory.CreateDbContext();

        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync(Ct));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
        // The model in code and the last migration describe the same schema.
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Seeding_again_does_not_duplicate_master_data()
    {
        await using var db = Factory.CreateDbContext();

        await DbSeeder.SeedDemoFactoryAsync(db, Ct);

        Assert.Equal(DemoFactory.Machines.Length, await db.Machines.CountAsync(Ct));
        Assert.Equal(DemoFactory.Shifts.Length, await db.Shifts.CountAsync(Ct));
        Assert.Equal(DemoFactory.Steps.Length, await db.ProcessSteps.CountAsync(Ct));
        Assert.Equal(DemoFactory.Operators.Length, await db.Operators.CountAsync(Ct));
        Assert.Equal(DemoFactory.Recipes.Length, await db.Recipes.CountAsync(Ct));
        Assert.Equal(DemoFactory.Recipes.Sum(r => r.Parameters.Length), await db.RecipeParameters.CountAsync(Ct));
    }

    [Fact]
    public async Task Unique_index_rejects_a_second_row_with_the_same_result_id()
    {
        using var client = Factory.CreateMachineClient("WB-01");
        await PostResultsAsync(client, TestData.Result());
        await using var db = Factory.CreateDbContext();

        var exception = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("""
            INSERT INTO InspectionResults
                (ResultId, UnitId, ProcessStepId, MachineId, RecipeId, OperatorId, Verdict, InspectedAt, ReceivedAt, CycleTimeMs)
            SELECT ResultId, UnitId, ProcessStepId, MachineId, RecipeId, OperatorId, Verdict, InspectedAt, ReceivedAt, CycleTimeMs
            FROM InspectionResults;
            """, Ct));

        Assert.Equal(2601, exception.Number);
    }

    [Theory]
    [InlineData("UPDATE InspectionResults SET Verdict = 9;")]
    [InlineData("UPDATE InspectionResults SET CycleTimeMs = -1;")]
    public async Task Check_constraints_reject_invalid_values(string sql)
    {
        using var client = Factory.CreateMachineClient("WB-01");
        await PostResultsAsync(client, TestData.Result());
        await using var db = Factory.CreateDbContext();

        var exception = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(sql, Ct));

        Assert.Equal(547, exception.Number); // constraint violation
    }

    [Fact]
    public async Task Foreign_key_keeps_a_machine_that_has_results()
    {
        using var client = Factory.CreateMachineClient("WB-01");
        await PostResultsAsync(client, TestData.Result());
        await using var db = Factory.CreateDbContext();

        var exception = await Assert.ThrowsAsync<SqlException>(
            () => db.Database.ExecuteSqlRawAsync("DELETE FROM Machines WHERE Code = 'WB-01';", Ct));

        Assert.Equal(547, exception.Number);
    }
}