namespace FabTrace.Contracts;

/// <summary>
/// User roles for authorization across the platform.
/// </summary>
public static class Roles
{
    public const string Operator = "Operator";
    public const string Engineer = "Engineer";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Operator, Engineer, Admin];
}