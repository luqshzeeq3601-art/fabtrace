using FabTrace.Contracts;
using FabTrace.Core;
using FabTrace.Core.Services;

using Microsoft.EntityFrameworkCore;

namespace FabTrace.Data.Services;

public sealed class ScadaService(FabTraceDbContext db) : IScadaService
{
    public async Task<LiveFloorSummaryDto> GetFloorSummaryAsync(CancellationToken cancellationToken = default)
    {
        var shiftStartUtc = FactoryTime.GetCurrentShiftStartUtc(DateTime.UtcNow);

        var machines = await db.Machines
            .OrderBy(m => m.Line)
            .ThenBy(m => m.Code)
            .ToListAsync(cancellationToken);

        var machineStatuses = new List<LiveMachineStatusDto>(machines.Count);

        foreach (var m in machines)
        {
            var latestState = await db.MachineStateEvents
                .Where(e => e.MachineId == m.MachineId)
                .OrderByDescending(e => e.StartedAt)
                .Select(e => new { e.State, e.StartedAt, e.ReasonCode })
                .FirstOrDefaultAsync(cancellationToken);

            var activeAlarms = await db.Alarms
                .Where(a => a.MachineId == m.MachineId && a.ClearedAt == null)
                .OrderByDescending(a => a.RaisedAt)
                .Select(a => new LiveAlarmDto(a.EventId, m.Code, a.AlarmCode, a.Severity, a.Message, a.RaisedAt))
                .ToListAsync(cancellationToken);

            var shiftResults = await db.InspectionResults
                .Where(r => r.MachineId == m.MachineId && r.InspectedAt >= shiftStartUtc)
                .GroupBy(r => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Passed = g.Count(r => r.Verdict == Verdict.Pass),
                    Failed = g.Count(r => r.Verdict == Verdict.Fail)
                })
                .FirstOrDefaultAsync(cancellationToken);

            var latestResult = await db.InspectionResults
                .Where(r => r.MachineId == m.MachineId)
                .OrderByDescending(r => r.InspectedAt)
                .Select(r => new
                {
                    LotNumber = r.Unit != null && r.Unit.Lot != null ? r.Unit.Lot.LotNumber : null,
                    RecipeCode = r.Recipe != null ? r.Recipe.Code : null,
                    OperatorBadge = r.Operator != null ? r.Operator.BadgeNo : null
                })
                .FirstOrDefaultAsync(cancellationToken);

            var total = shiftResults?.Total ?? 0;
            var passed = shiftResults?.Passed ?? 0;
            var failed = shiftResults?.Failed ?? 0;
            var yieldPct = total > 0 ? Math.Round(passed * 100.0 / total, 2) : 100.0;

            machineStatuses.Add(new LiveMachineStatusDto(
                m.Code,
                m.Name,
                m.MachineType,
                m.Line,
                m.IsActive,
                latestState?.State ?? MachineState.Idle,
                latestState?.StartedAt ?? DateTime.UtcNow,
                latestState?.ReasonCode,
                activeAlarms,
                total,
                passed,
                failed,
                yieldPct,
                latestResult?.LotNumber,
                latestResult?.RecipeCode,
                latestResult?.OperatorBadge));
        }

        var running = machineStatuses.Count(m => m.CurrentState == MachineState.Running);
        var idle = machineStatuses.Count(m => m.CurrentState == MachineState.Idle);
        var down = machineStatuses.Count(m => m.CurrentState == MachineState.Down);
        var other = machineStatuses.Count(m => m.CurrentState is MachineState.Setup or MachineState.Maintenance);

        var totalActiveAlarms = machineStatuses.Sum(m => m.ActiveAlarms.Count);
        var criticalAlarms = machineStatuses.Sum(m => m.ActiveAlarms.Count(a => a.Severity == AlarmSeverity.Critical));

        var totalInspected = machineStatuses.Sum(m => m.ShiftUnitsInspected);
        var totalPassed = machineStatuses.Sum(m => m.ShiftUnitsPassed);
        var totalFailed = machineStatuses.Sum(m => m.ShiftUnitsFailed);
        var overallYield = totalInspected > 0 ? Math.Round(totalPassed * 100.0 / totalInspected, 2) : 100.0;

        return new LiveFloorSummaryDto(
            machineStatuses.Count,
            running,
            idle,
            down,
            other,
            totalActiveAlarms,
            criticalAlarms,
            totalInspected,
            totalPassed,
            totalFailed,
            overallYield,
            machineStatuses);
    }

    public async Task<LiveMachineStatusDto?> GetMachineLiveStatusAsync(string machineCode, CancellationToken cancellationToken = default)
    {
        var floor = await GetFloorSummaryAsync(cancellationToken);
        return floor.Machines.FirstOrDefault(m => m.MachineCode.Equals(machineCode, StringComparison.OrdinalIgnoreCase));
    }
}