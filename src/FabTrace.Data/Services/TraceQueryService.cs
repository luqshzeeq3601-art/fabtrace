using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.EntityFrameworkCore;

namespace FabTrace.Data.Services;

/// <summary>
/// Read-only queries. Each one projects straight to the columns it needs with
/// Select, so SQL Server never returns whole rows that are then thrown away,
/// and nothing is change-tracked.
/// </summary>
public sealed class TraceQueryService(FabTraceDbContext db) : ITraceQueryService
{
    public async Task<IReadOnlyList<MachineDto>> GetMachinesAsync(CancellationToken cancellationToken) =>
        await db.Machines
            .OrderBy(m => m.Code)
            .Select(m => new MachineDto(m.Code, m.Name, m.MachineType, m.Line, m.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<LotSummaryDto?> GetLotAsync(string lotNumber, CancellationToken cancellationToken)
    {
        var lot = await db.Lots
            .Where(l => l.LotNumber == lotNumber)
            .Select(l => new
            {
                l.LotNumber,
                l.ProductCode,
                l.StartedAt,
                UnitCount = l.Units.Count,
                ResultCount = l.Units.SelectMany(u => u.Results).Count(),
                FailCount = l.Units.SelectMany(u => u.Results).Count(r => r.Verdict == Verdict.Fail),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return lot is null
            ? null
            : new LotSummaryDto(lot.LotNumber, lot.ProductCode, lot.StartedAt, lot.UnitCount, lot.ResultCount, lot.FailCount);
    }

    public async Task<UnitTraceDto?> GetUnitAsync(string serialNumber, CancellationToken cancellationToken)
    {
        var unit = await db.Units
            .Where(u => u.SerialNumber == serialNumber)
            .Select(u => new
            {
                u.SerialNumber,
                u.Lot!.LotNumber,
                u.Lot.ProductCode,
                Results = u.Results
                    .OrderBy(r => r.InspectedAt)
                    .Select(r => new
                    {
                        r.ResultId,
                        StepCode = r.ProcessStep!.Code,
                        MachineCode = r.Machine!.Code,
                        RecipeCode = r.Recipe!.Code,
                        RecipeVersion = r.Recipe.Version,
                        r.Operator!.BadgeNo,
                        r.Verdict,
                        r.DefectCode,
                        r.InspectedAt,
                        Readings = r.Readings
                            .Select(t => new TestReadingDto
                            {
                                ParameterName = t.ParameterName,
                                Value = t.Value,
                                LowerLimit = t.LowerLimit,
                                UpperLimit = t.UpperLimit,
                                Unit = t.Unit,
                            })
                            .ToList(),
                    })
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (unit is null)
        {
            return null;
        }

        var results = unit.Results
            .Select(r => new UnitResultDto(
                r.ResultId, r.StepCode, r.MachineCode, r.RecipeCode, r.RecipeVersion,
                r.BadgeNo, r.Verdict, r.DefectCode, r.InspectedAt, r.Readings))
            .ToList();

        return new UnitTraceDto(unit.SerialNumber, unit.LotNumber, unit.ProductCode, results);
    }
}