using System.Net;
using System.Net.Http.Json;

using FabTrace.Contracts;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class AdminApiKeyTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Admin_can_issue_new_api_key_and_machine_can_immediately_authenticate()
    {
        using var adminClient = Factory.CreateUserClient("admin1", Roles.Admin);

        var response = await adminClient.PostAsync("api/admin/machines/WB-02/api-key", null, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var keyResponse = await response.Content.ReadFromJsonAsync<IssueApiKeyResponse>(Ct);
        Assert.NotNull(keyResponse);
        Assert.Equal("WB-02", keyResponse.MachineCode);
        Assert.StartsWith("key_wb-02_", keyResponse.RawApiKey);

        // Verify machine can now authenticate with the newly issued key
        using var machineClient = Factory.CreateClient();
        machineClient.DefaultRequestHeaders.Add(ApiConventions.ApiKeyHeader, keyResponse.RawApiKey);

        var ingestResponse = await machineClient.PostAsJsonAsync(
            ApiConventions.InspectionResultsRoute,
            new[] { TestData.Result() },
            Ct);

        Assert.Equal(HttpStatusCode.OK, ingestResponse.StatusCode);
    }

    [Fact]
    public async Task Non_admin_user_is_forbidden_from_issuing_keys()
    {
        using var operatorClient = Factory.CreateUserClient("operator1", Roles.Operator);

        var response = await operatorClient.PostAsync("api/admin/machines/WB-01/api-key", null, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_is_unauthorized()
    {
        using var anonymousClient = Factory.CreateClient();

        var response = await anonymousClient.PostAsync("api/admin/machines/WB-01/api-key", null, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}