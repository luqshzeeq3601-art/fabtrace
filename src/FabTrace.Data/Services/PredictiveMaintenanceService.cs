using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.TimeSeries;

namespace FabTrace.Data.Services;

/// <summary>
/// ML.NET time-series anomaly detection and change-point analysis for predictive machine maintenance.
/// Evaluates live and historical sensor/test readings to detect drift and flag degradation before failure.
/// </summary>
public sealed class PredictiveMaintenanceService(FabTraceDbContext db) : IPredictiveMaintenanceService
{
    private sealed class MetricInput
    {
        public float Value { get; set; }
    }

    private sealed class ChangePointOutput
    {
        [VectorType(4)]
        public double[] Prediction { get; set; } = default!;
    }

    private sealed class SpikeOutput
    {
        [VectorType(3)]
        public double[] Prediction { get; set; } = default!;
    }

    public async Task<PredictiveMaintenanceReportDto> AnalyzeParameterAsync(
        string machineCode,
        string parameterName,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var machine = await db.Machines
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Code == machineCode, cancellationToken);

        if (machine is null)
        {
            throw new KeyNotFoundException($"Machine '{machineCode}' was not found.");
        }

        var readings = await db.InspectionResults
            .AsNoTracking()
            .Where(ir => ir.MachineId == machine.MachineId
                      && ir.InspectedAt >= fromUtc
                      && ir.InspectedAt <= toUtc)
            .SelectMany(ir => ir.Readings
                .Where(tr => tr.ParameterName == parameterName)
                .Select(tr => new
                {
                    ir.InspectedAt,
                    tr.Value,
                    tr.LowerLimit,
                    tr.UpperLimit
                }))
            .OrderBy(r => r.InspectedAt)
            .ToListAsync(cancellationToken);

        if (readings.Count < 10)
        {
            var fallbackPoints = readings.Select(r => new AnomalyDetectionItemDto(
                r.InspectedAt,
                r.Value,
                IsAnomaly: false,
                IsChangePoint: false,
                AnomalyScore: r.Value,
                PValue: 1.0,
                MartingaleScore: 0.0)).ToList();

            var mean = readings.Count > 0 ? readings.Average(r => r.Value) : 0.0;
            return new PredictiveMaintenanceReportDto(
                MachineCode: machine.Code,
                MachineName: machine.Name,
                ParameterName: parameterName,
                TotalSamples: readings.Count,
                AnomalyCount: 0,
                ChangePointDetected: false,
                FirstChangePointTime: null,
                BaselineMean: Math.Round(mean, 3),
                CurrentMean: Math.Round(mean, 3),
                DriftPercentage: 0.0,
                HealthStatus: "InsufficientData",
                Recommendation: "Insufficient time-series samples (minimum 10 required for ML.NET analysis).",
                DataPoints: fallbackPoints);
        }

        // ML.NET Pipeline Setup
        var mlContext = new MLContext(seed: 42);
        var inputs = readings.Select(r => new MetricInput { Value = (float)r.Value }).ToList();
        var dataView = mlContext.Data.LoadFromEnumerable(inputs);

        var historyLength = Math.Clamp(readings.Count / 5, 5, 25);

        // 1. Change Point Detection
        var cpPipeline = mlContext.Transforms.DetectIidChangePoint(
            outputColumnName: nameof(ChangePointOutput.Prediction),
            inputColumnName: nameof(MetricInput.Value),
            confidence: 95.0,
            changeHistoryLength: historyLength);

        var cpModel = cpPipeline.Fit(dataView);
        var cpTransformed = cpModel.Transform(dataView);
        var cpPredictions = mlContext.Data.CreateEnumerable<ChangePointOutput>(cpTransformed, reuseRowObject: false).ToList();

        // 2. Spike Anomaly Detection
        var spikePipeline = mlContext.Transforms.DetectIidSpike(
            outputColumnName: nameof(SpikeOutput.Prediction),
            inputColumnName: nameof(MetricInput.Value),
            confidence: 98.0,
            pvalueHistoryLength: historyLength);

        var spikeModel = spikePipeline.Fit(dataView);
        var spikeTransformed = spikeModel.Transform(dataView);
        var spikePredictions = mlContext.Data.CreateEnumerable<SpikeOutput>(spikeTransformed, reuseRowObject: false).ToList();

        var dataPoints = new List<AnomalyDetectionItemDto>(readings.Count);
        DateTime? firstChangePoint = null;
        int anomalyCount = 0;

        for (int i = 0; i < readings.Count; i++)
        {
            var r = readings[i];
            var cp = cpPredictions[i].Prediction;
            var sp = spikePredictions[i].Prediction;

            bool isCp = cp.Length > 0 && cp[0] == 1.0;
            bool isSp = sp.Length > 0 && sp[0] == 1.0;
            bool isAnomaly = isCp || isSp;

            if (isAnomaly) anomalyCount++;
            if (isCp && firstChangePoint is null)
            {
                firstChangePoint = r.InspectedAt;
            }

            dataPoints.Add(new AnomalyDetectionItemDto(
                Timestamp: r.InspectedAt,
                Value: r.Value,
                IsAnomaly: isAnomaly,
                IsChangePoint: isCp,
                AnomalyScore: cp.Length > 1 ? Math.Round(cp[1], 4) : r.Value,
                PValue: cp.Length > 2 ? Math.Round(cp[2], 4) : 1.0,
                MartingaleScore: cp.Length > 3 ? Math.Round(cp[3], 4) : 0.0));
        }

        // Calculate baseline vs recent drift
        int window = Math.Max(3, readings.Count / 5);
        double baseline = readings.Take(window).Average(r => r.Value);
        double current = readings.TakeLast(window).Average(r => r.Value);
        double driftPct = baseline != 0 ? Math.Round(((current - baseline) / Math.Abs(baseline)) * 100.0, 2) : 0.0;

        double? upper = readings.FirstOrDefault(r => r.UpperLimit.HasValue)?.UpperLimit;

        string healthStatus;
        string recommendation;

        if (firstChangePoint.HasValue)
        {
            if (Math.Abs(driftPct) >= 20.0 || (upper.HasValue && current >= upper.Value * 0.95))
            {
                healthStatus = "Critical";
                recommendation = $"CRITICAL DEGRADATION: ML.NET detected persistent structural change point at {firstChangePoint.Value:yyyy-MM-dd HH:mm} UTC with a {driftPct:+0.0;-0.0}% drift in '{parameterName}'. " +
                    (machineCode.StartsWith("FT") ? "Test socket wear suspected. Immediately replace socket to prevent catastrophic line stoppage." : "Urgent maintenance and re-calibration required.");
            }
            else
            {
                healthStatus = "Degrading";
                recommendation = $"DEGRADATION WARNING: Change point detected at {firstChangePoint.Value:yyyy-MM-dd HH:mm} UTC ({driftPct:+0.0;-0.0}% drift). Plan predictive maintenance in upcoming shift.";
            }
        }
        else if (anomalyCount > 0)
        {
            healthStatus = "Warning";
            recommendation = $"STOCHASTIC SPIKES: Detected {anomalyCount} transient spikes in '{parameterName}'. Values remain within safe control bounds; continue monitoring.";
        }
        else
        {
            healthStatus = "Healthy";
            recommendation = $"STABLE: Parameter '{parameterName}' exhibits nominal statistical stability. No predictive maintenance needed.";
        }

        return new PredictiveMaintenanceReportDto(
            MachineCode: machine.Code,
            MachineName: machine.Name,
            ParameterName: parameterName,
            TotalSamples: readings.Count,
            AnomalyCount: anomalyCount,
            ChangePointDetected: firstChangePoint.HasValue,
            FirstChangePointTime: firstChangePoint,
            BaselineMean: Math.Round(baseline, 3),
            CurrentMean: Math.Round(current, 3),
            DriftPercentage: driftPct,
            HealthStatus: healthStatus,
            Recommendation: recommendation,
            DataPoints: dataPoints);
    }

    public async Task<IReadOnlyList<MachineHealthSummaryDto>> GetFleetHealthSummaryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var machines = await db.Machines
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.Code)
            .ToListAsync(cancellationToken);

        var summaries = new List<MachineHealthSummaryDto>();

        foreach (var m in machines)
        {
            var targetParams = m.MachineType switch
            {
                "Tester" => new[] { "ContactResistance", "LeakageCurrent" },
                "WireBonder" => new[] { "PullStrength", "BallShear" },
                _ => new[] { "Coplanarity", "MarkContrast" }
            };

            var monitoredParams = new List<string>();
            string worstStatus = "Healthy";
            int totalAnomalies = 0;
            string summaryMsg = "All monitored parameters are stable.";

            foreach (var param in targetParams)
            {
                var count = await db.InspectionResults
                    .Where(ir => ir.MachineId == m.MachineId
                              && ir.InspectedAt >= fromUtc
                              && ir.InspectedAt <= toUtc)
                    .SelectMany(ir => ir.Readings.Where(tr => tr.ParameterName == param))
                    .CountAsync(cancellationToken);

                if (count > 0)
                {
                    monitoredParams.Add(param);
                    if (count >= 10)
                    {
                        var report = await AnalyzeParameterAsync(m.Code, param, fromUtc, toUtc, cancellationToken);
                        totalAnomalies += report.AnomalyCount;

                        if (report.HealthStatus == "Critical")
                        {
                            worstStatus = "Critical";
                            summaryMsg = report.Recommendation;
                        }
                        else if (report.HealthStatus == "Degrading" && worstStatus != "Critical")
                        {
                            worstStatus = "Degrading";
                            summaryMsg = report.Recommendation;
                        }
                        else if (report.HealthStatus == "Warning" && worstStatus == "Healthy")
                        {
                            worstStatus = "Warning";
                            summaryMsg = report.Recommendation;
                        }
                    }
                }
            }

            summaries.Add(new MachineHealthSummaryDto(
                MachineCode: m.Code,
                MachineName: m.Name,
                MachineType: m.MachineType,
                Line: m.Line,
                MonitoredParameters: monitoredParams,
                HealthStatus: worstStatus,
                AnomalyCount: totalAnomalies,
                SummaryMessage: summaryMsg,
                LastAssessedAt: DateTime.UtcNow));
        }

        return summaries;
    }
}