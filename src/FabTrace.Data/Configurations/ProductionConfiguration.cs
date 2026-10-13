using FabTrace.Contracts;
using FabTrace.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FabTrace.Data.Configurations;

// Deletes are Restrict everywhere except parent-owned detail rows (readings of a
// result). Production history must not disappear because someone deleted a machine.
//
// Only the indexes needed for correctness exist in phase 1: unique keys, and the
// foreign-key indexes EF Core adds by convention. Reporting indexes come in phase 3.

public class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.Property(l => l.LotNumber).HasMaxLength(FieldLengths.Code);
        builder.Property(l => l.ProductCode).HasMaxLength(FieldLengths.Code);
        builder.HasIndex(l => l.LotNumber).IsUnique();
    }
}

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.Property(u => u.SerialNumber).HasMaxLength(FieldLengths.Code);
        builder.HasIndex(u => u.SerialNumber).IsUnique();

        builder.HasOne(u => u.Lot).WithMany(l => l.Units).HasForeignKey(u => u.LotId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InspectionResultConfiguration : IEntityTypeConfiguration<InspectionResult>
{
    public void Configure(EntityTypeBuilder<InspectionResult> builder)
    {
        builder.Property(r => r.DefectCode).HasMaxLength(FieldLengths.Code);

        // The idempotency guarantee. Even if two copies of a result race past the
        // application's "already stored?" check, SQL Server refuses the second one.
        builder.HasIndex(r => r.ResultId).IsUnique();

        // Phase 3 reporting indexes: optimizes yield, defect Pareto, and time-window queries.
        builder.HasIndex(r => new { r.InspectedAt, r.MachineId });
        builder.HasIndex(r => new { r.RecipeId, r.Verdict });

        builder.HasOne(r => r.Unit).WithMany(u => u.Results).HasForeignKey(r => r.UnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ProcessStep).WithMany().HasForeignKey(r => r.ProcessStepId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Machine).WithMany().HasForeignKey(r => r.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Recipe).WithMany().HasForeignKey(r => r.RecipeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Operator).WithMany().HasForeignKey(r => r.OperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Readings).WithOne().HasForeignKey(t => t.InspectionResultId).OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_InspectionResults_Verdict", CheckConstraints.EnumValues<Verdict>("Verdict"));
            table.HasCheckConstraint("CK_InspectionResults_CycleTimeMs", "[CycleTimeMs] >= 0");
        });
    }
}

public class TestReadingConfiguration : IEntityTypeConfiguration<TestReading>
{
    public void Configure(EntityTypeBuilder<TestReading> builder)
    {
        builder.Property(t => t.ParameterName).HasMaxLength(FieldLengths.Code);
        builder.Property(t => t.Unit).HasMaxLength(FieldLengths.Unit);

        // Phase 3 reporting index: accelerates parametric trend and tester drift queries.
        builder.HasIndex(t => new { t.InspectionResultId, t.ParameterName });
    }
}

public class AlarmConfiguration : IEntityTypeConfiguration<Alarm>
{
    public void Configure(EntityTypeBuilder<Alarm> builder)
    {
        builder.Property(a => a.AlarmCode).HasMaxLength(FieldLengths.Code);
        builder.Property(a => a.Message).HasMaxLength(FieldLengths.Message);
        builder.HasIndex(a => a.EventId).IsUnique();

        // Phase 3 reporting index: accelerates machine alarm history and active alarm scans.
        builder.HasIndex(a => new { a.MachineId, a.RaisedAt });

        builder.HasOne(a => a.Machine).WithMany().HasForeignKey(a => a.MachineId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Alarms_Severity", CheckConstraints.EnumValues<AlarmSeverity>("Severity"));
            table.HasCheckConstraint("CK_Alarms_ClearedAfterRaised", "[ClearedAt] IS NULL OR [ClearedAt] >= [RaisedAt]");
        });
    }
}

public class MachineStateEventConfiguration : IEntityTypeConfiguration<MachineStateEvent>
{
    public void Configure(EntityTypeBuilder<MachineStateEvent> builder)
    {
        builder.Property(e => e.ReasonCode).HasMaxLength(FieldLengths.Code);
        builder.HasIndex(e => e.EventId).IsUnique();

        // Phase 3 reporting index: accelerates downtime and LEAD() window partition calculations.
        builder.HasIndex(e => new { e.MachineId, e.StartedAt });

        builder.HasOne(e => e.Machine).WithMany().HasForeignKey(e => e.MachineId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_MachineStateEvents_State", CheckConstraints.EnumValues<MachineState>("State")));
    }
}

internal static class CheckConstraints
{
    /// <summary>
    /// Builds "[Column] IN (1, 2, ...)" from the enum, so the database rejects
    /// numbers that are not a defined value even when rows are written by raw SQL.
    /// </summary>
    public static string EnumValues<TEnum>(string column) where TEnum : struct, Enum =>
        $"[{column}] IN ({string.Join(", ", Enum.GetValues<TEnum>().Select(value => Convert.ToInt32(value)))})";
}