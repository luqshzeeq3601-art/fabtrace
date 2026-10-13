using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabTrace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingIndexesAndProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TestReadings_InspectionResultId",
                table: "TestReadings");

            migrationBuilder.DropIndex(
                name: "IX_MachineStateEvents_MachineId",
                table: "MachineStateEvents");

            migrationBuilder.DropIndex(
                name: "IX_InspectionResults_RecipeId",
                table: "InspectionResults");

            migrationBuilder.DropIndex(
                name: "IX_Alarms_MachineId",
                table: "Alarms");

            migrationBuilder.CreateIndex(
                name: "IX_TestReadings_InspectionResultId_ParameterName",
                table: "TestReadings",
                columns: new[] { "InspectionResultId", "ParameterName" });

            migrationBuilder.CreateIndex(
                name: "IX_MachineStateEvents_MachineId_StartedAt",
                table: "MachineStateEvents",
                columns: new[] { "MachineId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionResults_InspectedAt_MachineId",
                table: "InspectionResults",
                columns: new[] { "InspectedAt", "MachineId" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionResults_RecipeId_Verdict",
                table: "InspectionResults",
                columns: new[] { "RecipeId", "Verdict" });

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_MachineId_RaisedAt",
                table: "Alarms",
                columns: new[] { "MachineId", "RaisedAt" });

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE sp_GetYieldReport
    @FromUtc datetime2(3),
    @ToUtc datetime2(3),
    @MachineCode nvarchar(32) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        m.Code AS MachineCode,
        ps.Code AS ProcessStepCode,
        r.Code AS RecipeCode,
        r.Version AS RecipeVersion,
        COUNT(ir.InspectionResultId) AS TotalUnits,
        SUM(CASE WHEN ir.Verdict = 1 THEN 1 ELSE 0 END) AS PassedUnits,
        SUM(CASE WHEN ir.Verdict = 2 THEN 1 ELSE 0 END) AS FailedUnits,
        ROUND(CAST(SUM(CASE WHEN ir.Verdict = 1 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(ir.InspectionResultId), 0) AS float), 2) AS YieldPercent
    FROM InspectionResults ir
    INNER JOIN Machines m ON ir.MachineId = m.MachineId
    INNER JOIN ProcessSteps ps ON ir.ProcessStepId = ps.ProcessStepId
    INNER JOIN Recipes r ON ir.RecipeId = r.RecipeId
    WHERE ir.InspectedAt >= @FromUtc AND ir.InspectedAt <= @ToUtc
      AND (@MachineCode IS NULL OR m.Code = @MachineCode)
    GROUP BY m.Code, ps.Code, r.Code, r.Version
    ORDER BY ps.Code, m.Code, r.Code, r.Version;
END;");

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE sp_GetMachineDowntimeReport
    @FromUtc datetime2(3),
    @ToUtc datetime2(3),
    @MachineCode nvarchar(32) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    WITH Transitions AS (
        SELECT
            mse.MachineId,
            mse.State,
            mse.ReasonCode,
            mse.StartedAt,
            LEAD(mse.StartedAt, 1, @ToUtc) OVER (PARTITION BY mse.MachineId ORDER BY mse.StartedAt) AS NextStartedAt
        FROM MachineStateEvents mse
        WHERE mse.StartedAt >= @FromUtc AND mse.StartedAt <= @ToUtc
    ),
    Durations AS (
        SELECT
            MachineId,
            State,
            ReasonCode,
            DATEDIFF(second, StartedAt, NextStartedAt) AS DurationSeconds
        FROM Transitions
    )
    SELECT
        m.Code AS MachineCode,
        ISNULL(SUM(CASE WHEN d.State = 1 THEN d.DurationSeconds ELSE 0 END), 0) AS RunningSeconds,
        ISNULL(SUM(CASE WHEN d.State = 2 THEN d.DurationSeconds ELSE 0 END), 0) AS IdleSeconds,
        ISNULL(SUM(CASE WHEN d.State = 3 THEN d.DurationSeconds ELSE 0 END), 0) AS DownSeconds,
        ISNULL(SUM(CASE WHEN d.State = 4 THEN d.DurationSeconds ELSE 0 END), 0) AS SetupSeconds,
        ISNULL(SUM(CASE WHEN d.State = 5 THEN d.DurationSeconds ELSE 0 END), 0) AS MaintenanceSeconds,
        ISNULL(SUM(d.DurationSeconds), 0) AS TotalTrackedSeconds
    FROM Machines m
    LEFT JOIN Durations d ON m.MachineId = d.MachineId
    WHERE (@MachineCode IS NULL OR m.Code = @MachineCode)
    GROUP BY m.Code
    ORDER BY m.Code;
END;");

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE sp_GetOeeReport
    @FromUtc datetime2(3),
    @ToUtc datetime2(3),
    @MachineCode nvarchar(32) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    WITH Transitions AS (
        SELECT
            mse.MachineId,
            mse.State,
            mse.StartedAt,
            LEAD(mse.StartedAt, 1, @ToUtc) OVER (PARTITION BY mse.MachineId ORDER BY mse.StartedAt) AS NextStartedAt
        FROM MachineStateEvents mse
        WHERE mse.StartedAt >= @FromUtc AND mse.StartedAt <= @ToUtc
    ),
    StateTotals AS (
        SELECT
            MachineId,
            ISNULL(SUM(CASE WHEN State = 1 THEN DATEDIFF(second, StartedAt, NextStartedAt) ELSE 0 END), 0) AS RunningSeconds,
            ISNULL(SUM(DATEDIFF(second, StartedAt, NextStartedAt)), 0) AS TotalSeconds
        FROM Transitions
        GROUP BY MachineId
    ),
    ProductionTotals AS (
        SELECT
            ir.MachineId,
            COUNT(ir.InspectionResultId) AS TotalUnits,
            SUM(CASE WHEN ir.Verdict = 1 THEN 1 ELSE 0 END) AS PassedUnits,
            SUM(CASE WHEN ir.Verdict = 2 THEN 1 ELSE 0 END) AS FailedUnits,
            AVG(CAST(ir.CycleTimeMs AS float)) AS AvgCycleTimeMs
        FROM InspectionResults ir
        WHERE ir.InspectedAt >= @FromUtc AND ir.InspectedAt <= @ToUtc
        GROUP BY ir.MachineId
    )
    SELECT
        m.Code AS MachineCode,
        m.Name AS MachineName,
        m.Line AS Line,
        ISNULL(st.RunningSeconds, 0) AS RunningSeconds,
        ISNULL(st.TotalSeconds, 0) AS TotalTrackedSeconds,
        ISNULL(pt.TotalUnits, 0) AS TotalProduced,
        ISNULL(pt.PassedUnits, 0) AS GoodProduced,
        ISNULL(pt.FailedUnits, 0) AS FailedProduced,
        ROUND(CAST(CASE WHEN ISNULL(st.TotalSeconds, 0) > 0 THEN ISNULL(st.RunningSeconds, 0) * 100.0 / st.TotalSeconds ELSE 0.0 END AS float), 2) AS AvailabilityPercent,
        ROUND(CAST(CASE WHEN ISNULL(pt.TotalUnits, 0) > 0 THEN ISNULL(pt.PassedUnits, 0) * 100.0 / pt.TotalUnits ELSE 100.0 END AS float), 2) AS QualityPercent,
        ROUND(CAST(CASE
            WHEN ISNULL(st.RunningSeconds, 0) > 0 AND ISNULL(pt.TotalUnits, 0) > 0
            THEN (ISNULL(pt.TotalUnits, 0) * (ISNULL(pt.AvgCycleTimeMs, 1000.0) / 1000.0)) * 100.0 / st.RunningSeconds
            WHEN ISNULL(pt.TotalUnits, 0) > 0 THEN 100.0
            ELSE 0.0
        END AS float), 2) AS PerformancePercent
    FROM Machines m
    LEFT JOIN StateTotals st ON m.MachineId = st.MachineId
    LEFT JOIN ProductionTotals pt ON m.MachineId = pt.MachineId
    WHERE (@MachineCode IS NULL OR m.Code = @MachineCode)
    ORDER BY m.Line, m.Code;
END;");

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE sp_GetDefectPareto
    @FromUtc datetime2(3),
    @ToUtc datetime2(3),
    @MachineCode nvarchar(32) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ISNULL(ir.DefectCode, 'UNKNOWN') AS DefectCode,
        COUNT(*) AS DefectCount,
        ROUND(CAST(COUNT(*) * 100.0 / NULLIF(SUM(COUNT(*)) OVER(), 0) AS float), 2) AS Percentage
    FROM InspectionResults ir
    INNER JOIN Machines m ON ir.MachineId = m.MachineId
    WHERE ir.Verdict = 2
      AND ir.InspectedAt >= @FromUtc AND ir.InspectedAt <= @ToUtc
      AND (@MachineCode IS NULL OR m.Code = @MachineCode)
    GROUP BY ir.DefectCode
    ORDER BY DefectCount DESC;
END;");

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE sp_GetParametricDrift
    @FromUtc datetime2(3),
    @ToUtc datetime2(3),
    @ParameterName nvarchar(32) = 'ContactResistance'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        m.Code AS MachineCode,
        CAST(ir.InspectedAt AS date) AS InspectionDate,
        COUNT(tr.TestReadingId) AS ReadingCount,
        ROUND(AVG(tr.Value), 4) AS AvgValue,
        ROUND(MIN(tr.Value), 4) AS MinValue,
        ROUND(MAX(tr.Value), 4) AS MaxValue,
        MAX(tr.UpperLimit) AS UpperLimit
    FROM TestReadings tr
    INNER JOIN InspectionResults ir ON tr.InspectionResultId = ir.InspectionResultId
    INNER JOIN Machines m ON ir.MachineId = m.MachineId
    WHERE tr.ParameterName = @ParameterName
      AND ir.InspectedAt >= @FromUtc AND ir.InspectedAt <= @ToUtc
    GROUP BY m.Code, CAST(ir.InspectedAt AS date)
    ORDER BY m.Code, InspectionDate;
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TestReadings_InspectionResultId_ParameterName",
                table: "TestReadings");

            migrationBuilder.DropIndex(
                name: "IX_MachineStateEvents_MachineId_StartedAt",
                table: "MachineStateEvents");

            migrationBuilder.DropIndex(
                name: "IX_InspectionResults_InspectedAt_MachineId",
                table: "InspectionResults");

            migrationBuilder.DropIndex(
                name: "IX_InspectionResults_RecipeId_Verdict",
                table: "InspectionResults");

            migrationBuilder.DropIndex(
                name: "IX_Alarms_MachineId_RaisedAt",
                table: "Alarms");

            migrationBuilder.CreateIndex(
                name: "IX_TestReadings_InspectionResultId",
                table: "TestReadings",
                column: "InspectionResultId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineStateEvents_MachineId",
                table: "MachineStateEvents",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionResults_RecipeId",
                table: "InspectionResults",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_MachineId",
                table: "Alarms",
                column: "MachineId");

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_GetYieldReport;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_GetMachineDowntimeReport;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_GetOeeReport;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_GetDefectPareto;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_GetParametricDrift;");
        }
    }
}