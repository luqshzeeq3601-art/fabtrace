using FabTrace.Api.Auth;
using FabTrace.Api.Hubs;
using FabTrace.Api.Services;
using FabTrace.Core.Services;
using FabTrace.Data;
using FabTrace.Data.Seeding;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog replaces the default logger; sinks and levels are set in appsettings.json.
builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddFabTraceData();

builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<FabTraceDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IUserTokenService, UserTokenService>();
builder.Services.AddSignalR();
builder.Services.AddScoped<IFactoryEventPublisher, SignalRFactoryEventPublisher>();

var ollamaEndpoint = builder.Configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
var ollamaModel = builder.Configuration["Ollama:Model"] ?? "llama3.1";
builder.Services.AddSingleton<Microsoft.Extensions.AI.IChatClient>(_ =>
    new OllamaSharp.OllamaApiClient(new Uri(ollamaEndpoint), ollamaModel));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5176", "https://localhost:7176"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = UserTokenAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = UserTokenAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null)
    .AddScheme<AuthenticationSchemeOptions, UserTokenAuthenticationHandler>(UserTokenAuthenticationHandler.SchemeName, null);

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Development only: bring the database up to date and load the demo plant.
    // Anywhere else, migrations are applied deliberately with `dotnet ef database update`.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FabTraceDbContext>();
    db.Database.SetCommandTimeout(300);
    await db.Database.MigrateAsync();
    await DbSeeder.SeedDemoFactoryAsync(db);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await DbSeeder.SeedIdentityAsync(userManager, roleManager);
}

// One log line per request, tagged with the machine that sent it.
app.UseSerilogRequestLogging(options =>
    options.EnrichDiagnosticContext = (diagnostics, http) =>
        diagnostics.Set("MachineCode", http.User.GetMachineCode() ?? "-"));

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<FactoryHub>("/hubs/factory");

app.MapGet("/health", async (FabTraceDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "Healthy" })
        : Results.Json(new { status = "Unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable));

app.Run();

// Lets the integration tests start this application in-process.
public partial class Program;