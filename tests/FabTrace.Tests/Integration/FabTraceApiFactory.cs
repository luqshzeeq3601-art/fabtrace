using FabTrace.Contracts;
using FabTrace.Core.Demo;
using FabTrace.Data;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FabTrace.Tests.Integration;

/// <summary>
/// Starts the real API in-process against a real SQL Server database.
/// The database is dropped and rebuilt from the migrations once per test run.
/// </summary>
public sealed class FabTraceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Local SQL Server Express by default; CI overrides it with an environment variable.</summary>
    public static string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("FABTRACE_TEST_CONNECTION")
        ?? @"Server=.\SQLEXPRESS;Database=FabTrace_Test;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", ConnectionString);

    public async ValueTask InitializeAsync()
    {
        var database = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
        if (!database.EndsWith("_Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to drop '{database}': the test database name must end with _Test.");
        }

        await using (var db = CreateDbContext())
        {
            await db.Database.EnsureDeletedAsync();
        }

        // Creating a client starts the application, which applies the migrations
        // and seeds the demo plant because the test host runs as Development.
        using var client = CreateClient();
    }

    public FabTraceDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FabTraceDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>A client that authenticates as one of the seeded demo machines.</summary>
    public HttpClient CreateMachineClient(string machineCode)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ApiConventions.ApiKeyHeader, DemoFactory.DevApiKey(machineCode));
        return client;
    }

    /// <summary>A client that authenticates as an interactive user with specified roles.</summary>
    public HttpClient CreateUserClient(string userName = "engineer1", params string[] roles)
    {
        var client = CreateClient();
        var tokenService = Services.GetRequiredService<FabTrace.Api.Auth.IUserTokenService>();
        var userRoles = roles.Length > 0 ? roles : [Roles.Engineer];
        var token = tokenService.CreateToken(userName, userRoles);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Removes everything machines have sent, keeping the master data.</summary>
    public async Task ResetProductionDataAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM TestReadings;
            DELETE FROM InspectionResults;
            DELETE FROM Units;
            DELETE FROM Lots;
            DELETE FROM Alarms;
            DELETE FROM MachineStateEvents;
            """);
    }
}

/// <summary>
/// Every integration test class joins this collection: they share one running
/// API and one database, and xUnit runs them one after another, not in parallel.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<FabTraceApiFactory>
{
    public const string Name = "Database";
}