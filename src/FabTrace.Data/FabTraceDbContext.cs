using FabTrace.Core.Entities;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FabTrace.Data;

public class FabTraceDbContext(DbContextOptions<FabTraceDbContext> options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options)
{
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<ProcessStep> ProcessSteps => Set<ProcessStep>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeParameter> RecipeParameters => Set<RecipeParameter>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<InspectionResult> InspectionResults => Set<InspectionResult>();
    public DbSet<TestReading> TestReadings => Set<TestReading>();
    public DbSet<Alarm> Alarms => Set<Alarm>();
    public DbSet<MachineStateEvent> MachineStateEvents => Set<MachineStateEvent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every timestamp is UTC with millisecond precision. datetime2(3) is 7 bytes;
        // the default datetime2(7) is 8 and keeps digits no machine clock can be trusted for.
        configurationBuilder.Properties<DateTime>()
            .HaveColumnType("datetime2(3)")
            .HaveConversion<UtcDateTimeConverter>();

        configurationBuilder.Properties<TimeOnly>().HaveColumnType("time(0)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FabTraceDbContext).Assembly);
    }

    /// <summary>
    /// SQL Server does not store a time zone with datetime2, so values come back
    /// as "unspecified". Marking them UTC makes JSON output end in "Z".
    /// </summary>
    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        toDatabase => toDatabase,
        fromDatabase => DateTime.SpecifyKind(fromDatabase, DateTimeKind.Utc));
}