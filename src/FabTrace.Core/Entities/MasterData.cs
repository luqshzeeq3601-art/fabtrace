namespace FabTrace.Core.Entities;

// Master data: set up by people before production runs. Small tables that change rarely.

public class Machine
{
    public int MachineId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string MachineType { get; set; }
    public required string Line { get; set; }

    /// <summary>SHA-256 of the machine's API key. The key itself is never stored.</summary>
    public required byte[] ApiKeyHash { get; set; }

    public bool IsActive { get; set; } = true;
}

public class Shift
{
    public int ShiftId { get; set; }
    public required string Name { get; set; }

    /// <summary>Factory local time. A shift may run past midnight (23:00 to 07:00).</summary>
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public bool Covers(TimeOnly localTime) => localTime.IsBetween(StartTime, EndTime);
}

public class Operator
{
    public int OperatorId { get; set; }
    public required string BadgeNo { get; set; }
    public required string Name { get; set; }

    public int ShiftId { get; set; }
    public Shift? Shift { get; set; }
}

public class ProcessStep
{
    public int ProcessStepId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }

    /// <summary>Position in the route: 1 is the first step a unit goes through.</summary>
    public int Sequence { get; set; }
}

/// <summary>A recipe is immutable once released: a change is a new version, not an edit.</summary>
public class Recipe
{
    public int RecipeId { get; set; }
    public required string Code { get; set; }
    public int Version { get; set; }
    public required string ProductCode { get; set; }

    public List<RecipeParameter> Parameters { get; set; } = [];
}

public class RecipeParameter
{
    public int RecipeParameterId { get; set; }
    public int RecipeId { get; set; }
    public required string Name { get; set; }
    public double Target { get; set; }
    public double? LowerLimit { get; set; }
    public double? UpperLimit { get; set; }
    public required string Unit { get; set; }
}