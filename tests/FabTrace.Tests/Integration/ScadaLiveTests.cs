using System.Net;
using System.Net.Http.Json;

using FabTrace.Contracts;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class ScadaLiveTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Theory]
    [InlineData("api/scada/live")]
    [InlineData("api/scada/machines/FT-02")]
    public async Task Scada_endpoints_require_authentication(string url)
    {
        using var client = Factory.CreateClient();
        using var response = await client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignalR_hub_rejects_unauthenticated_connection()
    {
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, "hubs/factory"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        await Assert.ThrowsAnyAsync<HttpRequestException>(async () =>
        {
            await hubConnection.StartAsync(Ct);
        });
    }

    [Fact]
    public async Task GetLiveSummary_returns_seeded_plant_machines_and_counts()
    {
        using var client = Factory.CreateUserClient();

        var summary = await client.GetFromJsonAsync<LiveFloorSummaryDto>("api/scada/live", Ct);

        Assert.NotNull(summary);
        Assert.Equal(6, summary.TotalMachines);
        Assert.Equal(6, summary.Machines.Count);
        Assert.All(summary.Machines, m => Assert.False(string.IsNullOrWhiteSpace(m.MachineCode)));
    }

    [Fact]
    public async Task Ingesting_state_and_alarm_updates_live_scada_view()
    {
        using var client = Factory.CreateMachineClient("FT-02");

        var eventId = Guid.NewGuid();
        var stateTime = DateTime.UtcNow;

        // Post state transition to Down
        await client.PostAsJsonAsync(ApiConventions.MachineStatesRoute, new[]
        {
            new MachineStateEventDto
            {
                EventId = eventId,
                State = MachineState.Down,
                StartedAt = stateTime,
                ReasonCode = "SOCKET_JAM"
            }
        }, Ct);

        // Post an active alarm
        var alarmId = Guid.NewGuid();
        await client.PostAsJsonAsync(ApiConventions.AlarmsRoute, new[]
        {
            new AlarmDto
            {
                EventId = alarmId,
                AlarmCode = "ERR_SOCKET_JAM",
                Severity = AlarmSeverity.Critical,
                Message = "Tester socket contact jammed",
                RaisedAt = stateTime
            }
        }, Ct);

        // Read back SCADA status
        using var queryClient = Factory.CreateUserClient();
        var machineStatus = await queryClient.GetFromJsonAsync<LiveMachineStatusDto>("api/scada/machines/FT-02", Ct);

        Assert.NotNull(machineStatus);
        Assert.Equal(MachineState.Down, machineStatus.CurrentState);
        Assert.Equal("SOCKET_JAM", machineStatus.StateReasonCode);
        Assert.Contains(machineStatus.ActiveAlarms, a => a.AlarmCode == "ERR_SOCKET_JAM" && a.Severity == AlarmSeverity.Critical);
    }

    [Fact]
    public async Task SignalR_broadcasts_events_to_connected_clients()
    {
        var tokenService = Factory.Services.GetRequiredService<FabTrace.Api.Auth.IUserTokenService>();
        var token = tokenService.CreateToken("operator1", [Roles.Operator]);

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, "hubs/factory"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

        var stateTcs = new TaskCompletionSource<MachineStateBroadcastDto>();
        hubConnection.On<MachineStateBroadcastDto>("MachineStateChanged", dto =>
        {
            if (dto.MachineCode == "WB-01")
            {
                stateTcs.TrySetResult(dto);
            }
        });

        await hubConnection.StartAsync(Ct);

        try
        {
            using var machineClient = Factory.CreateMachineClient("WB-01");
            var stateEvent = new MachineStateEventDto
            {
                EventId = Guid.NewGuid(),
                State = MachineState.Running,
                StartedAt = DateTime.UtcNow,
                ReasonCode = "RUNNING_BATCH"
            };

            var postResponse = await machineClient.PostAsJsonAsync(
                ApiConventions.MachineStatesRoute,
                new[] { stateEvent },
                Ct);

            Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

            // Wait for SignalR broadcast
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await stateTcs.Task.WaitAsync(timeoutCts.Token);

            Assert.Equal("WB-01", received.MachineCode);
            Assert.Equal(MachineState.Running, received.State);
            Assert.Equal("RUNNING_BATCH", received.ReasonCode);
        }
        finally
        {
            await hubConnection.StopAsync(Ct);
            await hubConnection.DisposeAsync();
        }
    }
}