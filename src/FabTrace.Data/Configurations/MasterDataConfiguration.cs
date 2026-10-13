using FabTrace.Contracts;
using FabTrace.Core.Entities;
using FabTrace.Core.Security;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FabTrace.Data.Configurations;

// Pattern used for every table: an int/bigint identity primary key that other
// tables point to, plus a unique index on the "natural key" people actually use
// (machine code, badge number, lot number...).

public class MachineConfiguration : IEntityTypeConfiguration<Machine>
{
    public void Configure(EntityTypeBuilder<Machine> builder)
    {
        builder.Property(m => m.Code).HasMaxLength(FieldLengths.Code);
        builder.Property(m => m.Name).HasMaxLength(FieldLengths.Name);
        builder.Property(m => m.MachineType).HasMaxLength(FieldLengths.Code);
        builder.Property(m => m.Line).HasMaxLength(FieldLengths.Code);
        builder.Property(m => m.ApiKeyHash).HasColumnType($"binary({ApiKeyHasher.HashSizeBytes})");

        builder.HasIndex(m => m.Code).IsUnique();
        // Authentication looks a machine up by key hash on every request.
        builder.HasIndex(m => m.ApiKeyHash).IsUnique();
    }
}

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(FieldLengths.Code);
        builder.HasIndex(s => s.Name).IsUnique();
    }
}

public class OperatorConfiguration : IEntityTypeConfiguration<Operator>
{
    public void Configure(EntityTypeBuilder<Operator> builder)
    {
        builder.Property(o => o.BadgeNo).HasMaxLength(FieldLengths.Code);
        builder.Property(o => o.Name).HasMaxLength(FieldLengths.Name);
        builder.HasIndex(o => o.BadgeNo).IsUnique();

        builder.HasOne(o => o.Shift).WithMany().HasForeignKey(o => o.ShiftId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProcessStepConfiguration : IEntityTypeConfiguration<ProcessStep>
{
    public void Configure(EntityTypeBuilder<ProcessStep> builder)
    {
        builder.Property(s => s.Code).HasMaxLength(FieldLengths.Code);
        builder.Property(s => s.Name).HasMaxLength(FieldLengths.Name);
        builder.HasIndex(s => s.Code).IsUnique();
    }
}

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.Property(r => r.Code).HasMaxLength(FieldLengths.Code);
        builder.Property(r => r.ProductCode).HasMaxLength(FieldLengths.Code);
        builder.HasIndex(r => new { r.Code, r.Version }).IsUnique();

        builder.HasMany(r => r.Parameters).WithOne().HasForeignKey(p => p.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RecipeParameterConfiguration : IEntityTypeConfiguration<RecipeParameter>
{
    public void Configure(EntityTypeBuilder<RecipeParameter> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(FieldLengths.Code);
        builder.Property(p => p.Unit).HasMaxLength(FieldLengths.Unit);
        builder.HasIndex(p => new { p.RecipeId, p.Name }).IsUnique();
    }
}