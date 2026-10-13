using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class PerformanceIndexTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Theory]
    [InlineData("InspectionResults", "IX_InspectionResults_InspectedAt_MachineId")]
    [InlineData("InspectionResults", "IX_InspectionResults_RecipeId_Verdict")]
    [InlineData("TestReadings", "IX_TestReadings_InspectionResultId_ParameterName")]
    [InlineData("MachineStateEvents", "IX_MachineStateEvents_MachineId_StartedAt")]
    [InlineData("Alarms", "IX_Alarms_MachineId_RaisedAt")]
    public async Task Reporting_indexes_exist_in_sql_server(string tableName, string indexName)
    {
        await using var db = Factory.CreateDbContext();

        var exists = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value
            FROM sys.indexes i
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            WHERE t.name = {tableName} AND i.name = {indexName}
            """).SingleAsync(Ct);

        Assert.Equal(1, exists);
    }

    [Fact]
    public async Task Time_range_query_plan_uses_reporting_index()
    {
        await using var db = Factory.CreateDbContext();
        var connection = (SqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(Ct);

        // Turn on execution plan XML capture
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SET SHOWPLAN_XML ON;";
            await cmd.ExecuteNonQueryAsync(Ct);
        }

        string planXml;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COUNT(*)
                FROM InspectionResults
                WHERE InspectedAt >= '2026-10-01' AND MachineId = 1;
                """;
            planXml = (string)(await cmd.ExecuteScalarAsync(Ct))!;
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SET SHOWPLAN_XML OFF;";
            await cmd.ExecuteNonQueryAsync(Ct);
        }

        // Verify the execution plan targets our Phase 3 reporting index
        Assert.Contains("IX_InspectionResults_InspectedAt_MachineId", planXml);
    }
}