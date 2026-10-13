using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.TimeSeries;

namespace FabTrace.Tests.Unit;

public class PredictiveMaintenanceTests
{
    private sealed class MetricObservation
    {
        public float Value { get; set; }
    }

    private sealed class ChangePointPrediction
    {
        [VectorType(4)]
        public double[] Prediction { get; set; } = default!;
    }

    private sealed class SpikePrediction
    {
        [VectorType(3)]
        public double[] Prediction { get; set; } = default!;
    }

    [Fact]
    public void MlNet_detects_step_change_point_in_time_series()
    {
        var mlContext = new MLContext(seed: 42);

        // 30 normal readings around 35.0, followed by a persistent step drift to 55.0
        var data = new List<MetricObservation>();
        var random = new Random(42);

        for (var i = 0; i < 30; i++)
        {
            data.Add(new MetricObservation { Value = (float)(35.0 + (random.NextDouble() - 0.5) * 2.0) });
        }

        for (var i = 0; i < 30; i++)
        {
            data.Add(new MetricObservation { Value = (float)(55.0 + (random.NextDouble() - 0.5) * 2.0) });
        }

        var dataView = mlContext.Data.LoadFromEnumerable(data);

        var pipeline = mlContext.Transforms.DetectIidChangePoint(
            outputColumnName: nameof(ChangePointPrediction.Prediction),
            inputColumnName: nameof(MetricObservation.Value),
            confidence: 95.0,
            changeHistoryLength: 15);

        var model = pipeline.Fit(dataView);
        var transformed = model.Transform(dataView);

        var predictions = mlContext.Data.CreateEnumerable<ChangePointPrediction>(transformed, reuseRowObject: false).ToList();

        Assert.Equal(60, predictions.Count);
        // An alert is 1 in index 0 of the prediction vector
        var alerts = predictions.Select((p, idx) => (p.Prediction[0], idx))
                                .Where(tuple => tuple.Item1 == 1)
                                .ToList();

        // Must detect a change point around index 30 (the start of the step shift)
        Assert.NotEmpty(alerts);
        Assert.Contains(alerts, a => a.idx >= 28 && a.idx <= 35);
    }

    [Fact]
    public void MlNet_detects_transient_spike_anomaly()
    {
        var mlContext = new MLContext(seed: 42);

        // 30 normal readings with one outlier spike at index 15
        var data = new List<MetricObservation>();
        var random = new Random(42);

        for (var i = 0; i < 30; i++)
        {
            var val = i == 15 ? 120.0f : (float)(35.0 + (random.NextDouble() - 0.5) * 2.0);
            data.Add(new MetricObservation { Value = val });
        }

        var dataView = mlContext.Data.LoadFromEnumerable(data);

        var pipeline = mlContext.Transforms.DetectIidSpike(
            outputColumnName: nameof(SpikePrediction.Prediction),
            inputColumnName: nameof(MetricObservation.Value),
            confidence: 99.0,
            pvalueHistoryLength: 10);

        var model = pipeline.Fit(dataView);
        var transformed = model.Transform(dataView);

        var predictions = mlContext.Data.CreateEnumerable<SpikePrediction>(transformed, reuseRowObject: false).ToList();

        Assert.Equal(30, predictions.Count);
        Assert.Equal(1, predictions[15].Prediction[0]); // Alert at index 15
    }
}