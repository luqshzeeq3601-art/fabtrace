using FabTrace.Contracts;
using FabTrace.Core.Entities;
using FabTrace.Core.Services;

using Microsoft.EntityFrameworkCore;

namespace FabTrace.Data.Services;

/// <summary>
/// Stores machine data exactly once.
/// </summary>
/// <remarks>
/// Idempotency works in two layers:
/// 1. Before inserting, ask the database which ids it already has and skip those.
/// 2. If another request inserts the same id between that check and our insert,
///    the unique index rejects our insert. We then start again, and this time
///    step 1 sees the other request's rows.
/// Each attempt is a single SaveChanges, which EF Core runs in one transaction,
/// so a batch is stored completely or not at all.
/// </remarks>
public sealed class IngestionService(
    FabTraceDbContext db,
    TimeProvider clock,
    IFactoryEventPublisher? publisher = null) : IIngestionService
{
    private const int MaxAttempts = 3;

    public Task<IngestResponse> IngestInspectionResultsAsync(
        int machineId, IReadOnlyList<InspectionResultDto> results, CancellationToken cancellationToken) =>
        RetryOnUniqueConflictAsync(() => StoreInspectionResultsAsync(machineId, results, cancellationToken));

    public Task<IngestResponse> IngestAlarmsAsync(
        int machineId, IReadOnlyList<AlarmDto> alarms, CancellationToken cancellationToken) =>
        RetryOnUniqueConflictAsync(() => StoreAlarmsAsync(machineId, alarms, cancellationToken));

    public Task<IngestResponse> IngestMachineStatesAsync(
        int machineId, IReadOnlyList<MachineStateEventDto> events, CancellationToken cancellationToken) =>
        RetryOnUniqueConflictAsync(() => StoreMachineStatesAsync(machineId, events, cancellationToken));

    public async Task<ClearAlarmOutcome> ClearAlarmAsync(
        int machineId, Guid eventId, DateTimeOffset clearedAt, CancellationToken cancellationToken)
    {
        // Filtering on machine as well as id means a machine can only clear its own alarms.
        var alarm = await db.Alarms.SingleOrDefaultAsync(
            a => a.EventId == eventId && a.MachineId == machineId, cancellationToken);

        if (alarm is null)
        {
            return ClearAlarmOutcome.NotFound;
        }

        if (alarm.ClearedAt is not null)
        {
            return ClearAlarmOutcome.Cleared;
        }

        if (clearedAt.UtcDateTime < alarm.RaisedAt)
        {
            return ClearAlarmOutcome.ClearedBeforeRaised;
        }

        alarm.ClearedAt = clearedAt.UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);

        if (publisher is not null)
        {
            var machineCode = await db.Machines
                .Where(m => m.MachineId == machineId)
                .Select(m => m.Code)
                .FirstOrDefaultAsync(cancellationToken);

            if (machineCode is not null)
            {
                await publisher.PublishAlarmClearedAsync(machineCode, eventId, clearedAt.UtcDateTime, cancellationToken);
            }
        }

        return ClearAlarmOutcome.Cleared;
    }

    private async Task<IngestResponse> StoreInspectionResultsAsync(
        int machineId, IReadOnlyList<InspectionResultDto> results, CancellationToken cancellationToken)
    {
        var receivedAt = clock.GetUtcNow().UtcDateTime;
        var alreadyStored = await FindStoredResultIdsAsync(results, cancellationToken);
        var reference = await LoadReferenceDataAsync(results, cancellationToken);
        var outcomes = new List<IngestItemResult>(results.Count);

        foreach (var dto in results)
        {
            // HashSet.Add returns false for an id we have seen, which also catches
            // the same result appearing twice inside one batch.
            if (!alreadyStored.Add(dto.ResultId))
            {
                outcomes.Add(new IngestItemResult(dto.ResultId, IngestStatus.Duplicate));
                continue;
            }

            var error = reference.Validate(dto);
            if (error is not null)
            {
                alreadyStored.Remove(dto.ResultId);
                outcomes.Add(new IngestItemResult(dto.ResultId, IngestStatus.Rejected, error));
                continue;
            }

            db.InspectionResults.Add(BuildResult(machineId, dto, reference, receivedAt));
            outcomes.Add(new IngestItemResult(dto.ResultId, IngestStatus.Created));
        }

        await db.SaveChangesAsync(cancellationToken);

        if (publisher is not null)
        {
            var machineCode = await db.Machines
                .Where(m => m.MachineId == machineId)
                .Select(m => m.Code)
                .FirstOrDefaultAsync(cancellationToken);

            if (machineCode is not null)
            {
                for (var i = 0; i < results.Count; i++)
                {
                    if (outcomes[i].Status == IngestStatus.Created)
                    {
                        var dto = results[i];
                        await publisher.PublishInspectionResultAsync(
                            machineCode,
                            dto.Verdict,
                            dto.SerialNumber,
                            dto.LotNumber,
                            dto.InspectedAt.UtcDateTime,
                            cancellationToken);
                    }
                }
            }
        }

        return new IngestResponse { Items = outcomes };
    }

    private async Task<HashSet<Guid>> FindStoredResultIdsAsync(
        IReadOnlyList<InspectionResultDto> results, CancellationToken cancellationToken)
    {
        var ids = results.Select(r => r.ResultId).Distinct().ToList();
        var stored = await db.InspectionResults
            .Where(r => ids.Contains(r.ResultId))
            .Select(r => r.ResultId)
            .ToListAsync(cancellationToken);
        return stored.ToHashSet();
    }

    /// <summary>Loads, in a handful of queries, every row the batch refers to by code.</summary>
    private async Task<ReferenceData> LoadReferenceDataAsync(
        IReadOnlyList<InspectionResultDto> results, CancellationToken cancellationToken)
    {
        var stepCodes = results.Select(r => r.ProcessStepCode).Distinct().ToList();
        var recipeCodes = results.Select(r => r.RecipeCode).Distinct().ToList();
        var badges = results.Select(r => r.OperatorBadge).Distinct().ToList();
        var lotNumbers = results.Select(r => r.LotNumber).Distinct().ToList();
        var serials = results.Select(r => r.SerialNumber).Distinct().ToList();

        var steps = await db.ProcessSteps.Where(s => stepCodes.Contains(s.Code)).ToListAsync(cancellationToken);
        var recipes = await db.Recipes.Where(r => recipeCodes.Contains(r.Code)).ToListAsync(cancellationToken);
        var operators = await db.Operators.Where(o => badges.Contains(o.BadgeNo)).ToListAsync(cancellationToken);
        var lots = await db.Lots.Where(l => lotNumbers.Contains(l.LotNumber)).ToListAsync(cancellationToken);
        var units = await db.Units.Include(u => u.Lot).Where(u => serials.Contains(u.SerialNumber)).ToListAsync(cancellationToken);

        return new ReferenceData(steps, recipes, operators, lots, units);
    }

    private InspectionResult BuildResult(
        int machineId, InspectionResultDto dto, ReferenceData reference, DateTime receivedAt)
    {
        var inspectedAt = dto.InspectedAt.UtcDateTime;
        var recipe = reference.Recipes[ReferenceData.RecipeKey(dto.RecipeCode, dto.RecipeVersion)];

        // Lots and units are created the first time any machine reports them.
        if (!reference.Lots.TryGetValue(dto.LotNumber, out var lot))
        {
            lot = new Lot { LotNumber = dto.LotNumber, ProductCode = recipe.ProductCode, StartedAt = inspectedAt };
            reference.Lots.Add(lot.LotNumber, lot);
            db.Lots.Add(lot);
        }
        else if (inspectedAt < lot.StartedAt)
        {
            // Results can arrive out of order after a machine has been offline.
            lot.StartedAt = inspectedAt;
        }

        if (!reference.Units.TryGetValue(dto.SerialNumber, out var unit))
        {
            unit = new Unit { SerialNumber = dto.SerialNumber, Lot = lot };
            reference.Units.Add(unit.SerialNumber, unit);
            db.Units.Add(unit);
        }

        return new InspectionResult
        {
            ResultId = dto.ResultId,
            Unit = unit,
            ProcessStepId = reference.Steps[dto.ProcessStepCode].ProcessStepId,
            MachineId = machineId,
            RecipeId = recipe.RecipeId,
            OperatorId = reference.Operators[dto.OperatorBadge].OperatorId,
            Verdict = dto.Verdict,
            DefectCode = dto.DefectCode,
            InspectedAt = inspectedAt,
            ReceivedAt = receivedAt,
            CycleTimeMs = dto.CycleTimeMs,
            Readings = dto.Readings.Select(reading => new TestReading
            {
                ParameterName = reading.ParameterName,
                Value = reading.Value,
                LowerLimit = reading.LowerLimit,
                UpperLimit = reading.UpperLimit,
                Unit = reading.Unit,
            }).ToList(),
        };
    }

    private async Task<IngestResponse> StoreAlarmsAsync(
        int machineId, IReadOnlyList<AlarmDto> alarms, CancellationToken cancellationToken)
    {
        var ids = alarms.Select(a => a.EventId).Distinct().ToList();
        var alreadyStored = (await db.Alarms
            .Where(a => ids.Contains(a.EventId))
            .Select(a => a.EventId)
            .ToListAsync(cancellationToken)).ToHashSet();
        var outcomes = new List<IngestItemResult>(alarms.Count);

        foreach (var dto in alarms)
        {
            if (!alreadyStored.Add(dto.EventId))
            {
                outcomes.Add(new IngestItemResult(dto.EventId, IngestStatus.Duplicate));
                continue;
            }

            db.Alarms.Add(new Alarm
            {
                EventId = dto.EventId,
                MachineId = machineId,
                AlarmCode = dto.AlarmCode,
                Severity = dto.Severity,
                Message = dto.Message,
                RaisedAt = dto.RaisedAt.UtcDateTime,
            });
            outcomes.Add(new IngestItemResult(dto.EventId, IngestStatus.Created));
        }

        await db.SaveChangesAsync(cancellationToken);

        if (publisher is not null)
        {
            var machineCode = await db.Machines
                .Where(m => m.MachineId == machineId)
                .Select(m => m.Code)
                .FirstOrDefaultAsync(cancellationToken);

            if (machineCode is not null)
            {
                for (var i = 0; i < alarms.Count; i++)
                {
                    if (outcomes[i].Status == IngestStatus.Created)
                    {
                        var dto = alarms[i];
                        await publisher.PublishAlarmRaisedAsync(
                            new LiveAlarmDto(dto.EventId, machineCode, dto.AlarmCode, dto.Severity, dto.Message, dto.RaisedAt.UtcDateTime),
                            cancellationToken);
                    }
                }
            }
        }

        return new IngestResponse { Items = outcomes };
    }

    private async Task<IngestResponse> StoreMachineStatesAsync(
        int machineId, IReadOnlyList<MachineStateEventDto> events, CancellationToken cancellationToken)
    {
        var ids = events.Select(e => e.EventId).Distinct().ToList();
        var alreadyStored = (await db.MachineStateEvents
            .Where(e => ids.Contains(e.EventId))
            .Select(e => e.EventId)
            .ToListAsync(cancellationToken)).ToHashSet();
        var outcomes = new List<IngestItemResult>(events.Count);

        foreach (var dto in events)
        {
            if (!alreadyStored.Add(dto.EventId))
            {
                outcomes.Add(new IngestItemResult(dto.EventId, IngestStatus.Duplicate));
                continue;
            }

            db.MachineStateEvents.Add(new MachineStateEvent
            {
                EventId = dto.EventId,
                MachineId = machineId,
                State = dto.State,
                StartedAt = dto.StartedAt.UtcDateTime,
                ReasonCode = dto.ReasonCode,
            });
            outcomes.Add(new IngestItemResult(dto.EventId, IngestStatus.Created));
        }

        await db.SaveChangesAsync(cancellationToken);

        if (publisher is not null)
        {
            var machineCode = await db.Machines
                .Where(m => m.MachineId == machineId)
                .Select(m => m.Code)
                .FirstOrDefaultAsync(cancellationToken);

            if (machineCode is not null)
            {
                for (var i = 0; i < events.Count; i++)
                {
                    if (outcomes[i].Status == IngestStatus.Created)
                    {
                        var dto = events[i];
                        await publisher.PublishMachineStateChangedAsync(
                            machineCode,
                            dto.State,
                            dto.StartedAt.UtcDateTime,
                            dto.ReasonCode,
                            cancellationToken);
                    }
                }
            }
        }

        return new IngestResponse { Items = outcomes };
    }

    private async Task<IngestResponse> RetryOnUniqueConflictAsync(Func<Task<IngestResponse>> store)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await store();
            }
            catch (DbUpdateException exception) when (SqlErrors.IsUniqueViolation(exception) && attempt < MaxAttempts)
            {
                // Another request stored one of our rows first. Nothing of this attempt
                // was saved, so forget the pending inserts and start again.
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>
    /// The rows a batch refers to, keyed by the codes used in the payload.
    /// Lookups ignore case because SQL Server's default collation does too.
    /// </summary>
    private sealed class ReferenceData(
        List<ProcessStep> steps, List<Recipe> recipes, List<Operator> operators, List<Lot> lots, List<Unit> units)
    {
        public Dictionary<string, ProcessStep> Steps { get; } =
            steps.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Recipe> Recipes { get; } =
            recipes.ToDictionary(r => RecipeKey(r.Code, r.Version), StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Operator> Operators { get; } =
            operators.ToDictionary(o => o.BadgeNo, StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Lot> Lots { get; } =
            lots.ToDictionary(l => l.LotNumber, StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Unit> Units { get; } =
            units.ToDictionary(u => u.SerialNumber, StringComparer.OrdinalIgnoreCase);

        public static string RecipeKey(string code, int version) => $"{code}#{version}";

        /// <returns>Why the result cannot be stored, or null when it can.</returns>
        public string? Validate(InspectionResultDto dto)
        {
            if (!Steps.ContainsKey(dto.ProcessStepCode))
            {
                return $"Unknown process step '{dto.ProcessStepCode}'.";
            }

            if (!Recipes.ContainsKey(RecipeKey(dto.RecipeCode, dto.RecipeVersion)))
            {
                return $"Unknown recipe '{dto.RecipeCode}' version {dto.RecipeVersion}.";
            }

            if (!Operators.ContainsKey(dto.OperatorBadge))
            {
                return $"Unknown operator badge '{dto.OperatorBadge}'.";
            }

            // A serial number identifies one physical unit, and a unit never changes lot.
            if (Units.TryGetValue(dto.SerialNumber, out var unit) &&
                !string.Equals(unit.Lot!.LotNumber, dto.LotNumber, StringComparison.OrdinalIgnoreCase))
            {
                return $"Serial '{dto.SerialNumber}' already belongs to lot '{unit.Lot.LotNumber}'.";
            }

            return null;
        }
    }
}