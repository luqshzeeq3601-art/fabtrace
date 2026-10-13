using FabTrace.Core.Demo;
using FabTrace.Core.Entities;
using FabTrace.Core.Security;

using Microsoft.EntityFrameworkCore;

namespace FabTrace.Data.Seeding;

/// <summary>
/// Inserts the demo plant's master data. Safe to run on every start: each row is
/// matched on its natural key and only added when missing.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedDemoFactoryAsync(FabTraceDbContext db, CancellationToken cancellationToken = default)
    {
        var shifts = await db.Shifts.ToDictionaryAsync(s => s.Name, cancellationToken);
        foreach (var demo in DemoFactory.Shifts.Where(d => !shifts.ContainsKey(d.Name)))
        {
            var shift = new Shift { Name = demo.Name, StartTime = demo.Start, EndTime = demo.End };
            shifts.Add(shift.Name, shift);
            db.Shifts.Add(shift);
        }

        var stepCodes = await db.ProcessSteps.Select(s => s.Code).ToListAsync(cancellationToken);
        foreach (var demo in DemoFactory.Steps.Where(d => !stepCodes.Contains(d.Code)))
        {
            db.ProcessSteps.Add(new ProcessStep { Code = demo.Code, Name = demo.Name, Sequence = demo.Sequence });
        }

        var machineCodes = await db.Machines.Select(m => m.Code).ToListAsync(cancellationToken);
        foreach (var demo in DemoFactory.Machines.Where(d => !machineCodes.Contains(d.Code)))
        {
            db.Machines.Add(new Machine
            {
                Code = demo.Code,
                Name = demo.Name,
                MachineType = demo.MachineType,
                Line = demo.Line,
                ApiKeyHash = ApiKeyHasher.Hash(DemoFactory.DevApiKey(demo.Code)),
            });
        }

        var badges = await db.Operators.Select(o => o.BadgeNo).ToListAsync(cancellationToken);
        foreach (var demo in DemoFactory.Operators.Where(d => !badges.Contains(d.BadgeNo)))
        {
            db.Operators.Add(new Operator { BadgeNo = demo.BadgeNo, Name = demo.Name, Shift = shifts[demo.ShiftName] });
        }

        var recipes = await db.Recipes.Select(r => new { r.Code, r.Version }).ToListAsync(cancellationToken);
        foreach (var demo in DemoFactory.Recipes.Where(d => !recipes.Contains(new { d.Code, d.Version })))
        {
            db.Recipes.Add(new Recipe
            {
                Code = demo.Code,
                Version = demo.Version,
                ProductCode = demo.ProductCode,
                Parameters = demo.Parameters.Select(p => new RecipeParameter
                {
                    Name = p.Name,
                    Target = p.Target,
                    LowerLimit = p.Lower,
                    UpperLimit = p.Upper,
                    Unit = p.Unit,
                }).ToList(),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task SeedIdentityAsync(
        Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser> userManager,
        Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole> roleManager)
    {
        foreach (var role in FabTrace.Contracts.Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole(role));
            }
        }

        var demoUsers = new (string UserName, string Role, string Password)[]
        {
            ("operator1", FabTrace.Contracts.Roles.Operator, "Operator123!"),
            ("engineer1", FabTrace.Contracts.Roles.Engineer, "Engineer123!"),
            ("admin1", FabTrace.Contracts.Roles.Admin, "Admin123!"),
        };

        foreach (var (userName, role, password) in demoUsers)
        {
            var user = await userManager.FindByNameAsync(userName);
            if (user is null)
            {
                user = new Microsoft.AspNetCore.Identity.IdentityUser
                {
                    UserName = userName,
                    Email = $"{userName}@fabtrace.local",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }
    }
}