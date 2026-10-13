using System.Net;
using System.Net.Http.Json;
using System.Text;

using FabTrace.Contracts;

using Microsoft.EntityFrameworkCore;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class InspectionResultIngestionTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task New_result_creates_lot_unit_result_and_readings()
    {
        using var client = Factory.CreateMachineClient("WB-01");
        var dto = TestData.Result();

        var response = await PostResultsAsync(client, dto);

        Assert.Equal(IngestStatus.Created, Assert.Single(response.Items).Status);

        await using var db = Factory.CreateDbContext();
        var stored = await db.InspectionResults
            .Include(r => r.Unit!.Lot)
            .Include(r => r.Machine)
            .Include(r => r.Readings)
            .SingleAsync(Ct);
        Assert.Equal(dto.ResultId, stored.ResultId);
        Assert.Equal("LOT-T1-001", stored.Unit!.SerialNumber);
        Assert.Equal("LOT-T1", stored.Unit.Lot!.LotNumber);
        Assert.Equal("QFN32", stored.Unit.Lot.ProductCode);
        Assert.Equal("WB-01", stored.Machine!.Code);
        Assert.Equal(dto.InspectedAt.UtcDateTime, stored.InspectedAt);
        Assert.Equal(2, stored.Readings.Count);
    }

    [Fact]
    public async Task Sending_the_same_result_twice_stores_it_once()
    {
        using var client = Factory.CreateMachineClient("WB-01");
        var dto = TestData.Result();

        var first = await PostResultsAsync(client, dto);
        var second = await PostResultsAsync(client, dto);

        Assert.Equal(IngestStatus.Created, first.Items[0].Status);
        Assert.Equal(IngestStatus.Duplicate, second.Items[0].Status);
        await AssertRowCountsAsync(lots: 1, units: 1, results: 1, readings: 2);
    }

    [Fact]
    public async Task Same_result_twice_in_one_batch_stores_it_once()
    {
        using var client = Factory.CreateMachineClient("WB-01");
        var dto = TestData.Result();

        var response = await PostResultsAsync(client, dto, dto);

        Assert.Equal([IngestStatus.Created, IngestStatus.Duplicate], response.Items.Select(i => i.Status));
        await AssertRowCountsAsync(lots: 1, units: 1, results: 1, readings: 2);
    }

    [Fact]
    public async Task Mixed_batch_reports_each_item_separately()
    {
        using var client = Factory.CreateMachineClient("WB-01");
        var existing = TestData.Result("LOT-T1-001");
        await PostResultsAsync(client, existing);

        var fresh = TestData.Result("LOT-T1-002");
        var wrongRecipe = TestData.Result("LOT-T1-003", recipeVersion: 99);
        var response = await PostResultsAsync(client, existing, fresh, wrongRecipe);

        Assert.Equal(
            [IngestStatus.Duplicate, IngestStatus.Created, IngestStatus.Rejected],
            response.Items.Select(i => i.Status));
        Assert.Contains("Unknown recipe", response.Items[2].Error);
        Assert.Equal((1, 1, 1), (response.Created, response.Duplicates, response.Rejected));
        await AssertRowCountsAsync(lots: 1, units: 2, results: 2, readings: 4);
    }

    [Fact]
    public async Task Identical_batches_sent_in_parallel_are_stored_exactly_once()
    {
        using var client = Factory.CreateMachineClient("WB-01");
        var batch = Enumerable.Range(1, 25).Select(n => TestData.Result($"LOT-T1-{n:D3}")).ToArray();

        // Eight senders race past the "already stored?" check with the same data.
        // The unique indexes make all but one insert fail, and those requests retry.
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PostResultsAsync(client, batch)));

        Assert.Equal(25, responses.Sum(r => r.Created));
        Assert.Equal(25 * 7, responses.Sum(r => r.Duplicates));
        await AssertRowCountsAsync(lots: 1, units: 25, results: 25, readings: 50);
    }

    [Fact]
    public async Task Machine_is_taken_from_the_api_key()
    {
        using var tester = Factory.CreateMachineClient("FT-01");

        await PostResultsAsync(tester, TestData.Result(processStepCode: "FINAL_TEST", recipeCode: "TEST-QFN32", operatorBadge: "OP102"));

        await using var db = Factory.CreateDbContext();
        Assert.Equal("FT-01", await db.InspectionResults.Select(r => r.Machine!.Code).SingleAsync(Ct));
    }

    [Theory]
    [InlineData("NO_SUCH_STEP", "OP101", "Unknown process step")]
    [InlineData("WIRE_BOND", "OP999", "Unknown operator badge")]
    public async Task Unknown_master_data_is_rejected(string stepCode, string badge, string expectedError)
    {
        using var client = Factory.CreateMachineClient("WB-01");

        var response = await PostResultsAsync(client, TestData.Result(processStepCode: stepCode, operatorBadge: badge));

        var item = Assert.Single(response.Items);
        Assert.Equal(IngestStatus.Rejected, item.Status);
        Assert.Contains(expectedError, item.Error);
        await AssertRowCountsAsync(lots: 0, units: 0, results: 0, readings: 0);
    }

    [Fact]
    public async Task Serial_number_cannot_move_to_another_lot()
    {
        using var client = Factory.CreateMachineClient("WB-01");
        await PostResultsAsync(client, TestData.Result("SN-1", lotNumber: "LOT-A"));

        var response = await PostResultsAsync(client, TestData.Result("SN-1", lotNumber: "LOT-B"));

        var item = Assert.Single(response.Items);
        Assert.Equal(IngestStatus.Rejected, item.Status);
        Assert.Contains("already belongs to lot 'LOT-A'", item.Error);
        await AssertRowCountsAsync(lots: 1, units: 1, results: 1, readings: 2);
    }

    [Fact]
    public async Task Lot_start_is_the_earliest_result_even_when_results_arrive_out_of_order()
    {
        using var client = Factory.CreateMachineClient("WB-01");
        var early = TestData.BaseTime;
        var late = early.AddHours(2);

        await PostResultsAsync(client, TestData.Result("LOT-T1-002", inspectedAt: late));
        await PostResultsAsync(client, TestData.Result("LOT-T1-001", inspectedAt: early));

        await using var db = Factory.CreateDbContext();
        Assert.Equal(early.UtcDateTime, (await db.Lots.SingleAsync(Ct)).StartedAt);
    }

    [Fact]
    public async Task Codes_are_matched_without_regard_to_case()
    {
        using var client = Factory.CreateMachineClient("WB-01");

        await PostResultsAsync(client, TestData.Result("LOT-T1-001", lotNumber: "LOT-T1"));
        var response = await PostResultsAsync(client, TestData.Result("lot-t1-002", lotNumber: "lot-t1", recipeCode: "bond-qfn32"));

        Assert.Equal(IngestStatus.Created, response.Items[0].Status);
        await AssertRowCountsAsync(lots: 1, units: 2, results: 2, readings: 4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ApiConventions.MaxBatchSize + 1)]
    public async Task Batch_outside_the_size_limits_is_a_bad_request(int size)
    {
        using var client = Factory.CreateMachineClient("WB-01");
        var batch = Enumerable.Range(1, size).Select(n => TestData.Result($"LOT-T1-{n:D4}")).ToArray();

        using var response = await client.PostAsJsonAsync(ApiConventions.InspectionResultsRoute, batch, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertRowCountsAsync(lots: 0, units: 0, results: 0, readings: 0);
    }

    [Theory]
    [InlineData("""[{ "resultId": "6f1d2c1e-0000-4000-8000-000000000001", "lotNumber": "LOT-T1" }]""")] // required fields missing
    [InlineData("""
        [{ "resultId": "6f1d2c1e-0000-4000-8000-000000000001", "lotNumber": "LOT-T1", "serialNumber": "", "processStepCode": "WIRE_BOND",
           "recipeCode": "BOND-QFN32", "recipeVersion": 1, "operatorBadge": "OP101", "verdict": "Pass", "inspectedAt": "2026-03-02T09:00:00+08:00" }]
        """)] // empty serial number
    [InlineData("""
        [{ "resultId": "6f1d2c1e-0000-4000-8000-000000000001", "lotNumber": "LOT-T1", "serialNumber": "SN-1", "processStepCode": "WIRE_BOND",
           "recipeCode": "BOND-QFN32", "recipeVersion": 1, "operatorBadge": "OP101", "verdict": 9, "inspectedAt": "2026-03-02T09:00:00+08:00" }]
        """)] // verdict is not a defined value
    public async Task Malformed_payload_is_a_bad_request(string json)
    {
        using var client = Factory.CreateMachineClient("WB-01");
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(ApiConventions.InspectionResultsRoute, content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertRowCountsAsync(lots: 0, units: 0, results: 0, readings: 0);
    }

    private async Task AssertRowCountsAsync(int lots, int units, int results, int readings)
    {
        await using var db = Factory.CreateDbContext();
        var actual = (
            await db.Lots.CountAsync(Ct),
            await db.Units.CountAsync(Ct),
            await db.InspectionResults.CountAsync(Ct),
            await db.TestReadings.CountAsync(Ct));
        Assert.Equal((lots, units, results, readings), actual);
    }
}