using System.Security.Cryptography;
using System.Text;

using FabTrace.Contracts;

namespace FabTrace.Generator.Simulation;

/// <summary>Something a simulated machine would send to the server.</summary>
public abstract record SimEvent(string MachineCode);

public sealed record ResultEvent(string MachineCode, InspectionResultDto Result) : SimEvent(MachineCode);

public sealed record StateEvent(string MachineCode, MachineStateEventDto State) : SimEvent(MachineCode);

public sealed record AlarmRaisedEvent(string MachineCode, AlarmDto Alarm) : SimEvent(MachineCode);

public sealed record AlarmClearedEvent(string MachineCode, Guid EventId, DateTimeOffset ClearedAt) : SimEvent(MachineCode);

public static class DeterministicGuid
{
    /// <summary>
    /// The same name always gives the same id. Event ids are built from what the
    /// event is ("result of serial X at step Y"), so replaying the history sends
    /// the same ids and the server recognises every item as a duplicate.
    /// </summary>
    public static Guid Create(string name) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(name)).AsSpan(0, 16));
}