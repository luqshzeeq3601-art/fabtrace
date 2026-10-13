using System.Net;
using System.Net.Http.Json;

using FabTrace.Contracts;

using Microsoft.EntityFrameworkCore;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class ApiKeyAuthTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    private static readonly InspectionResultDto[] OneResult = [TestData.Result()];

    [Fact]
    public async Task Request_without_api_key_is_unauthorized()
    {
        using var client = Factory.CreateClient();

        using var response = await client.PostAsJsonAsync(ApiConventions.InspectionResultsRoute, OneResult, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_with_unknown_api_key_is_unauthorized()
    {
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiConventions.ApiKeyHeader, "not-a-real-key");

        using var response = await client.PostAsJsonAsync(ApiConventions.InspectionResultsRoute, OneResult, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_with_valid_api_key_is_accepted()
    {
        using var client = Factory.CreateMachineClient("WB-01");

        using var response = await client.PostAsJsonAsync(ApiConventions.InspectionResultsRoute, OneResult, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_machine_is_unauthorized()
    {
        using var client = Factory.CreateMachineClient("VI-02");
        await SetActiveAsync("VI-02", isActive: false);

        try
        {
            using var response = await client.PostAsJsonAsync(ApiConventions.InspectionResultsRoute, OneResult, Ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await SetActiveAsync("VI-02", isActive: true);
        }
    }

    private async Task SetActiveAsync(string machineCode, bool isActive)
    {
        await using var db = Factory.CreateDbContext();
        await db.Machines
            .Where(m => m.Code == machineCode)
            .ExecuteUpdateAsync(update => update.SetProperty(m => m.IsActive, isActive), CancellationToken.None);
    }
}