using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FabTrace.Api.Hubs;

/// <summary>
/// SignalR hub for live SCADA updates: machine states, alarms, and inspection throughput.
/// </summary>
[Authorize]
public class FactoryHub : Hub;