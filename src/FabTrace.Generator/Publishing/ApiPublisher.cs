using System.Net.Http.Json;

using FabTrace.Contracts;
using FabTrace.Core.Demo;
using FabTrace.Generator.Simulation;

namespace FabTrace.Generator.Publishing;

/// <summary>
/// Sends simulated events to the FabTrace API the way real machines would: each
/// machine uses its own API key and posts in batches.
/// </summary>
public sealed class ApiPublisher(HttpClient http, int batchSize, Action<string>? log = null)
{
    private const int MaxAttempts = 3;
    private const int ProgressEveryResults = 20_000;

    private readonly Dictionary<string, MachineBuffer> _buffers = [];
    private int _nextProgressAt = ProgressEveryResults;

    public int Created { get; private set; }
    public int Duplicates { get; private set; }
    public int Rejected { get; private set; }
    public int ResultsSent { get; private set; }
    public List<string> RejectionSamples { get; } = [];

    public async Task PublishAsync(IEnumerable<SimEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var simEvent in events)
        {
            if (!_buffers.TryGetValue(simEvent.MachineCode, out var buffer))
            {
                _buffers.Add(simEvent.MachineCode, buffer = new MachineBuffer());
            }

            switch (simEvent)
            {
                case ResultEvent result: buffer.Results.Add(result.Result); break;
                case StateEvent state: buffer.States.Add(state.State); break;
                case AlarmRaisedEvent raised: buffer.Alarms.Add(raised.Alarm); break;
                case AlarmClearedEvent cleared: buffer.Clears.Add(cleared); break;
            }

            if (buffer.LargestCount >= batchSize)
            {
                await FlushAsync(simEvent.MachineCode, buffer, cancellationToken);
            }
        }

        foreach (var (machineCode, buffer) in _buffers)
        {
            await FlushAsync(machineCode, buffer, cancellationToken);
        }
    }

    private async Task FlushAsync(string machineCode, MachineBuffer buffer, CancellationToken cancellationToken)
    {
        await PostBatchAsync(machineCode, ApiConventions.MachineStatesRoute, buffer.States, cancellationToken);
        await PostBatchAsync(machineCode, ApiConventions.AlarmsRoute, buffer.Alarms, cancellationToken);

        // An alarm can only be cleared once the server knows it, so clears go after raises.
        foreach (var cleared in buffer.Clears)
        {
            var body = new ClearAlarmRequest { ClearedAt = cleared.ClearedAt };
            using var response = await SendAsync(machineCode, ApiConventions.ClearAlarmRoute(cleared.EventId), body, cancellationToken);
        }

        ResultsSent += buffer.Results.Count;
        await PostBatchAsync(machineCode, ApiConventions.InspectionResultsRoute, buffer.Results, cancellationToken);
        buffer.Clear();

        if (ResultsSent >= _nextProgressAt)
        {
            log?.Invoke($"  {ResultsSent:N0} results sent...");
            _nextProgressAt += ProgressEveryResults;
        }
    }

    private async Task PostBatchAsync<T>(string machineCode, string route, List<T> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        using var response = await SendAsync(machineCode, route, items, cancellationToken);
        var outcome = await response.Content.ReadFromJsonAsync<IngestResponse>(cancellationToken)
            ?? throw new InvalidOperationException($"Empty response from {route}.");

        Created += outcome.Created;
        Duplicates += outcome.Duplicates;
        Rejected += outcome.Rejected;
        RejectionSamples.AddRange(outcome.Items
            .Where(item => item.Status == IngestStatus.Rejected)
            .Select(item => $"{machineCode}: {item.Error}")
            .Take(Math.Max(0, 5 - RejectionSamples.Count)));
    }

    /// <summary>Posts with the machine's API key, retrying when the server is unreachable or answers 5xx.</summary>
    private async Task<HttpResponseMessage> SendAsync<T>(string machineCode, string route, T body, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
                request.Headers.Add(ApiConventions.ApiKeyHeader, DemoFactory.DevApiKey(machineCode));

                var response = await http.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                var status = (int)response.StatusCode;
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                response.Dispose();

                // 4xx means the request itself is wrong, so sending it again cannot help.
                if (status < 500 || attempt == MaxAttempts)
                {
                    throw new InvalidOperationException($"POST {route} as {machineCode} failed: {status} {detail}");
                }
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                // Server not reachable right now; fall through to the delay and try again.
            }

            await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
        }
    }

    private sealed class MachineBuffer
    {
        public List<InspectionResultDto> Results { get; } = [];
        public List<MachineStateEventDto> States { get; } = [];
        public List<AlarmDto> Alarms { get; } = [];
        public List<AlarmClearedEvent> Clears { get; } = [];

        public int LargestCount => Math.Max(Math.Max(Results.Count, States.Count), Math.Max(Alarms.Count, Clears.Count));

        public void Clear()
        {
            Results.Clear();
            States.Clear();
            Alarms.Clear();
            Clears.Clear();
        }
    }
}