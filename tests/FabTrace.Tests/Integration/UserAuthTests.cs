using System.Net;
using System.Net.Http.Json;

using FabTrace.Contracts;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class UserAuthTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Theory]
    [InlineData("operator1", "Operator123!", Roles.Operator)]
    [InlineData("engineer1", "Engineer123!", Roles.Engineer)]
    [InlineData("admin1", "Admin123!", Roles.Admin)]
    public async Task Login_with_correct_credentials_returns_token_and_role(
        string userName, string password, string expectedRole)
    {
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = password
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(Ct);
        Assert.NotNull(result);
        Assert.Equal(userName, result.UserName);
        Assert.Contains(expectedRole, result.Roles);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_unauthorized()
    {
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/auth/login", new LoginRequest
        {
            UserName = "admin1",
            Password = "WrongPassword!"
        }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_current_user_profile_requires_bearer_token()
    {
        using var client = Factory.CreateClient();

        var anonymousResponse = await client.GetAsync("api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var authenticated = Factory.CreateUserClient("engineer1", Roles.Engineer);
        var authResponse = await authenticated.GetAsync("api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);

        var profile = await authResponse.Content.ReadFromJsonAsync<UserProfileDto>(Ct);
        Assert.NotNull(profile);
        Assert.Equal("engineer1", profile.UserName);
        Assert.Contains(Roles.Engineer, profile.Roles);
    }
}