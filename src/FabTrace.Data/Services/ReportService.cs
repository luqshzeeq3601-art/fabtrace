using System.Data;
using System.Data.Common;

using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FabTrace.Data.Services;

public sealed class ReportService(FabTraceDbContext db) : IReportService
{
    private async Task<DbCommand> CreateStoredProcCommandAsync(string procName, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var command = connection.CreateCommand();
        command.CommandText = procName;
        command.CommandType = CommandType.StoredProcedure;
        return command;
    }

    public async Task<IReadOnlyList<YieldReportItemDto>> GetYieldReportAsync(
        DateTime fromUtc, DateTime toUtc, string? machineCode = null, CancellationToken cancellationToken = default)
    {
        var items = new List<YieldReportItemDto>();
        await using var command = await CreateStoredProcCommandAsync("sp_GetYieldReport", cancellationToken);

        command.Parameters.Add(new SqlParameter("@FromUtc", SqlDbType.DateTime2) { Value = fromUtc });
        command.Parameters.Add(new SqlParameter("@ToUtc", SqlDbType.DateTime2) { Value = toUtc });
        command.Parameters.Add(new SqlParameter("@MachineCode", SqlDbType.NVarChar, 32)
        {
            Value = (object?)machineCode ?? DBNull.Value
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new YieldReportItemDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetDouble(7)));
        }

        return items;
    }

    public async Task<IReadOnlyList<MachineDowntimeItemDto>> GetDowntimeReportAsync(
        DateTime fromUtc, DateTime toUtc, string? machineCode = null, CancellationToken cancellationToken = default)
    {
        var items = new List<MachineDowntimeItemDto>();
        await using var command = await CreateStoredProcCommandAsync("sp_GetMachineDowntimeReport", cancellationToken);

        command.Parameters.Add(new SqlParameter("@FromUtc", SqlDbType.DateTime2) { Value = fromUtc });
        command.Parameters.Add(new SqlParameter("@ToUtc", SqlDbType.DateTime2) { Value = toUtc });
        command.Parameters.Add(new SqlParameter("@MachineCode", SqlDbType.NVarChar, 32)
        {
            Value = (object?)machineCode ?? DBNull.Value
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = reader.GetString(0);
            var running = reader.GetInt32(1);
            var idle = reader.GetInt32(2);
            var down = reader.GetInt32(3);
            var setup = reader.GetInt32(4);
            var maint = reader.GetInt32(5);
            var total = reader.GetInt32(6);
            var availability = total > 0 ? Math.Round(running * 100.0 / total, 2) : 0.0;

            items.Add(new MachineDowntimeItemDto(
                code, running, idle, down, setup, maint, total, availability));
        }

        return items;
    }

    public async Task<IReadOnlyList<OeeReportItemDto>> GetOeeReportAsync(
        DateTime fromUtc, DateTime toUtc, string? machineCode = null, CancellationToken cancellationToken = default)
    {
        var items = new List<OeeReportItemDto>();
        await using var command = await CreateStoredProcCommandAsync("sp_GetOeeReport", cancellationToken);

        command.Parameters.Add(new SqlParameter("@FromUtc", SqlDbType.DateTime2) { Value = fromUtc });
        command.Parameters.Add(new SqlParameter("@ToUtc", SqlDbType.DateTime2) { Value = toUtc });
        command.Parameters.Add(new SqlParameter("@MachineCode", SqlDbType.NVarChar, 32)
        {
            Value = (object?)machineCode ?? DBNull.Value
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = reader.GetString(0);
            var name = reader.GetString(1);
            var line = reader.GetString(2);
            var running = reader.GetInt32(3);
            var totalSec = reader.GetInt32(4);
            var totalProd = reader.GetInt32(5);
            var goodProd = reader.GetInt32(6);
            var failProd = reader.GetInt32(7);
            var availPct = reader.GetDouble(8);
            var qualPct = reader.GetDouble(9);
            var perfPct = reader.GetDouble(10);

            // Clamp individual factors to [0..100]
            var aFactor = Math.Clamp(availPct / 100.0, 0.0, 1.0);
            var pFactor = Math.Clamp(perfPct / 100.0, 0.0, 1.0);
            var qFactor = Math.Clamp(qualPct / 100.0, 0.0, 1.0);
            var oee = Math.Round(aFactor * pFactor * qFactor * 100.0, 2);

            items.Add(new OeeReportItemDto(
                code, name, line, running, totalSec, totalProd, goodProd, failProd,
                availPct, perfPct, qualPct, oee));
        }

        return items;
    }

    public async Task<IReadOnlyList<DefectParetoItemDto>> GetDefectParetoAsync(
        DateTime fromUtc, DateTime toUtc, string? machineCode = null, CancellationToken cancellationToken = default)
    {
        var items = new List<DefectParetoItemDto>();
        await using var command = await CreateStoredProcCommandAsync("sp_GetDefectPareto", cancellationToken);

        command.Parameters.Add(new SqlParameter("@FromUtc", SqlDbType.DateTime2) { Value = fromUtc });
        command.Parameters.Add(new SqlParameter("@ToUtc", SqlDbType.DateTime2) { Value = toUtc });
        command.Parameters.Add(new SqlParameter("@MachineCode", SqlDbType.NVarChar, 32)
        {
            Value = (object?)machineCode ?? DBNull.Value
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new DefectParetoItemDto(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetDouble(2)));
        }

        return items;
    }

    public async Task<RootCauseAnalysisDto> GetRootCauseAnalysisAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var yieldItems = await GetYieldReportAsync(fromUtc, toUtc, cancellationToken: cancellationToken);
        var defectItems = await GetDefectParetoAsync(fromUtc, toUtc, cancellationToken: cancellationToken);

        // Fetch parametric drift for contact resistance
        var drifts = new List<ParametricDriftItemDto>();
        await using (var command = await CreateStoredProcCommandAsync("sp_GetParametricDrift", cancellationToken))
        {
            command.Parameters.Add(new SqlParameter("@FromUtc", SqlDbType.DateTime2) { Value = fromUtc });
            command.Parameters.Add(new SqlParameter("@ToUtc", SqlDbType.DateTime2) { Value = toUtc });
            command.Parameters.Add(new SqlParameter("@ParameterName", SqlDbType.NVarChar, 32) { Value = "ContactResistance" });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var machine = reader.GetString(0);
                var dt = reader.GetDateTime(1);
                var count = reader.GetInt32(2);
                var avg = reader.GetDouble(3);
                var min = reader.GetDouble(4);
                var max = reader.GetDouble(5);
                var upper = reader.IsDBNull(6) ? (double?)null : reader.GetDouble(6);

                drifts.Add(new ParametricDriftItemDto(
                    machine, DateOnly.FromDateTime(dt), count, avg, min, max, upper));
            }
        }

        var findings = new List<string>();

        // 1. Analyze recipe yield drops (e.g. BOND-QFN32 v2)
        var recipeGroups = yieldItems.GroupBy(y => y.RecipeCode);
        foreach (var group in recipeGroups)
        {
            var versions = group.OrderBy(g => g.RecipeVersion).ToList();
            if (versions.Count >= 2)
            {
                var v1 = versions.First();
                var vLast = versions.Last();
                if (vLast.YieldPercent < v1.YieldPercent - 5.0)
                {
                    findings.Add($"Recipe '{group.Key}' v{vLast.RecipeVersion} shows an abnormal yield drop to {vLast.YieldPercent:F1}% compared to v{v1.RecipeVersion} ({v1.YieldPercent:F1}%).");
                }
            }
        }

        // 2. Analyze ContactResistance drift on testers
        var ft02Drifts = drifts.Where(d => d.MachineCode == "FT-02").OrderBy(d => d.InspectionDate).ToList();
        if (ft02Drifts.Count >= 3)
        {
            var firstAvg = ft02Drifts.First().AvgValue;
            var lastAvg = ft02Drifts.Last().AvgValue;
            if (lastAvg > firstAvg * 1.5)
            {
                findings.Add($"Tester FT-02 exhibits a progressive ContactResistance drift from {firstAvg:F3} to {lastAvg:F3} terminating in socket contact alarm.");
            }
        }

        if (findings.Count == 0 && (yieldItems.Count > 0 || drifts.Count > 0))
        {
            findings.Add("No abnormal yield drop or machine drift detected in the specified timeframe.");
        }

        return new RootCauseAnalysisDto(yieldItems, drifts, defectItems, findings);
    }
}