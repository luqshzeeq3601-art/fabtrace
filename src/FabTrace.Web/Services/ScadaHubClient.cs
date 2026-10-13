using FabTrace.Contracts;

using Microsoft.AspNetCore.SignalR.Client;

namespace FabTrace.Web.Services;

public sealed class ScadaHubClient : IAsyncDisposable
{
    private readonly HubConnection _connection;

    public event Action<MachineStateBroadcastDto>? OnMachineStateChanged;
    public event Action<LiveAlarmDto>? OnAlarmRaised;
    public event Action<AlarmClearedBroadcastDto>? OnAlarmCleared;
    public event Action<InspectionResultBroadcastDto>? OnInspectionResultReported;
    public event Action<string>? OnConnectionStatusChanged;

    public bool IsConnected => _connection.State == HubConnectionState.Connected;
    public string ConnectionState => _connection.State.ToString();

    public ScadaHubClient(IConfiguration configuration, UserSession session)
    {
        var apiBaseUrl = configuration["ApiBaseUrl"] ?? "http://localhost:5080";
        var hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/factory";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(session.Token);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<MachineStateBroadcastDto>("MachineStateChanged", dto =>
            OnMachineStateChanged?.Invoke(dto));

        _connection.On<LiveAlarmDto>("AlarmRaised", dto =>
            OnAlarmRaised?.Invoke(dto));

        _connection.On<AlarmClearedBroadcastDto>("AlarmCleared", dto =>
            OnAlarmCleared?.Invoke(dto));

        _connection.On<InspectionResultBroadcastDto>("InspectionResultReported", dto =>
            OnInspectionResultReported?.Invoke(dto));

        _connection.Reconnecting += _ =>
        {
            OnConnectionStatusChanged?.Invoke("Reconnecting");
            return Task.CompletedTask;
        };

        _connection.Reconnected += _ =>
        {
            OnConnectionStatusChanged?.Invoke("Connected");
            return Task.CompletedTask;
        };

        _connection.Closed += _ =>
        {
            OnConnectionStatusChanged?.Invoke("Disconnected");
            return Task.CompletedTask;
        };
    }

    public async Task StartAsync()
    {
        if (_connection.State == HubConnectionState.Disconnected)
        {
            try
            {
                await _connection.StartAsync();
                OnConnectionStatusChanged?.Invoke("Connected");
            }
            catch
            {
                OnConnectionStatusChanged?.Invoke("Disconnected");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}