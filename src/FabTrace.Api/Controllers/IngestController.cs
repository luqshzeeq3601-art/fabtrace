using FabTrace.Api.Auth;
using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabTrace.Api.Controllers;

/// <summary>
/// Endpoints machines post their data to. Every call needs a valid machine API key.
/// </summary>
/// <remarks>
/// [ApiController] validates the body against the attributes on the DTOs before
/// an action runs, and answers 400 by itself when the shape is wrong. Problems
/// with the content of a single item (for example an unknown recipe) do not fail
/// the request: that item comes back as Rejected and the rest are stored.
/// </remarks>
[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
public class IngestController(IIngestionService ingestion) : ControllerBase
{
    [HttpPost(ApiConventions.InspectionResultsRoute)]
    public async Task<ActionResult<IngestResponse>> PostInspectionResults(
        IReadOnlyList<InspectionResultDto> results, CancellationToken cancellationToken)
    {
        if (BatchSizeProblem(results.Count) is { } problem)
        {
            return problem;
        }

        return await ingestion.IngestInspectionResultsAsync(User.GetMachineId(), results, cancellationToken);
    }

    [HttpPost(ApiConventions.AlarmsRoute)]
    public async Task<ActionResult<IngestResponse>> PostAlarms(
        IReadOnlyList<AlarmDto> alarms, CancellationToken cancellationToken)
    {
        if (BatchSizeProblem(alarms.Count) is { } problem)
        {
            return problem;
        }

        return await ingestion.IngestAlarmsAsync(User.GetMachineId(), alarms, cancellationToken);
    }

    [HttpPost(ApiConventions.AlarmsRoute + "/{eventId:guid}/clear")]
    public async Task<IActionResult> ClearAlarm(
        Guid eventId, ClearAlarmRequest request, CancellationToken cancellationToken)
    {
        var outcome = await ingestion.ClearAlarmAsync(User.GetMachineId(), eventId, request.ClearedAt, cancellationToken);

        return outcome switch
        {
            ClearAlarmOutcome.Cleared => NoContent(),
            ClearAlarmOutcome.NotFound => NotFound(),
            ClearAlarmOutcome.ClearedBeforeRaised => Problem(
                "The clear time is earlier than the time the alarm was raised.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => throw new InvalidOperationException($"Unhandled outcome {outcome}."),
        };
    }

    [HttpPost(ApiConventions.MachineStatesRoute)]
    public async Task<ActionResult<IngestResponse>> PostMachineStates(
        IReadOnlyList<MachineStateEventDto> events, CancellationToken cancellationToken)
    {
        if (BatchSizeProblem(events.Count) is { } problem)
        {
            return problem;
        }

        return await ingestion.IngestMachineStatesAsync(User.GetMachineId(), events, cancellationToken);
    }

    private ObjectResult? BatchSizeProblem(int count) =>
        count is >= 1 and <= ApiConventions.MaxBatchSize
            ? null
            : Problem(
                $"A batch must contain between 1 and {ApiConventions.MaxBatchSize} items.",
                statusCode: StatusCodes.Status400BadRequest);
}