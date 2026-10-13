using FabTrace.Core.Services;
using FabTrace.Data.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FabTrace.Data;

public static class DependencyInjection
{
    public const string ConnectionStringName = "FabTrace";

    public static IServiceCollection AddFabTraceData(this IServiceCollection services)
    {
        // The connection string is read when the first DbContext is created, not here,
        // so the integration tests can point the same startup code at the test database.
        services.AddDbContext<FabTraceDbContext>((provider, options) =>
            options.UseSqlServer(
                provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName),
                sql => sql.CommandTimeout(180)));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IIngestionService, IngestionService>();
        services.AddScoped<ITraceQueryService, TraceQueryService>();
        services.AddScoped<IScadaService, ScadaService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IPredictiveMaintenanceService, PredictiveMaintenanceService>();
        services.AddScoped<IAiRootCauseAssistant, AiRootCauseAssistant>();
        return services;
    }
}