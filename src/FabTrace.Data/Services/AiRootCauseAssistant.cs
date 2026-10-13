using System.ComponentModel;
using System.Text.Json;

using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.Extensions.AI;

namespace FabTrace.Data.Services;

public sealed class AiRootCauseAssistant(
    IChatClient chatClient,
    IReportService reports,
    ITraceQueryService traceQuery,
    IPredictiveMaintenanceService predictiveMaintenance) : IAiRootCauseAssistant
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
    };

    public async Task<AiRootCauseResponseDto> AnalyzeAsync(
        AiRootCauseQueryDto query, CancellationToken cancellationToken = default)
    {
        var evidenceList = new List<AiDataEvidenceDto>();

        // 1. Build read-only C# tools wrapping trusted application services
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                async ([Description("Start of time range in UTC")] DateTime fromUtc,
                       [Description("End of time range in UTC")] DateTime toUtc,
                       [Description("Optional machine code filter e.g. WB-01, FT-02")] string? machineCode) =>
                {
                    var data = await reports.GetYieldReportAsync(fromUtc, toUtc, machineCode, cancellationToken);
                    var json = JsonSerializer.Serialize(data, JsonOpts);
                    evidenceList.Add(new AiDataEvidenceDto(
                        "GetYieldReport",
                        $"Yield report from {fromUtc:yyyy-MM-dd} to {toUtc:yyyy-MM-dd} for machine '{machineCode ?? "ALL"}' ({data.Count} records)",
                        json));
                    return json;
                },
                "GetYieldReport",
                "Retrieves production yield (total units, pass, fail, yield percentage) grouped by machine, step, and recipe version."),

            AIFunctionFactory.Create(
                async ([Description("Start of time range in UTC")] DateTime fromUtc,
                       [Description("End of time range in UTC")] DateTime toUtc,
                       [Description("Optional machine code filter")] string? machineCode) =>
                {
                    var data = await reports.GetDefectParetoAsync(fromUtc, toUtc, machineCode, cancellationToken);
                    var json = JsonSerializer.Serialize(data, JsonOpts);
                    evidenceList.Add(new AiDataEvidenceDto(
                        "GetDefectPareto",
                        $"Defect Pareto from {fromUtc:yyyy-MM-dd} to {toUtc:yyyy-MM-dd} ({data.Count} defect categories)",
                        json));
                    return json;
                },
                "GetDefectPareto",
                "Retrieves defect Pareto analysis ranking failure reasons and their percentage of total defects."),

            AIFunctionFactory.Create(
                async ([Description("Start of time range in UTC")] DateTime fromUtc,
                       [Description("End of time range in UTC")] DateTime toUtc,
                       [Description("Optional machine code filter")] string? machineCode) =>
                {
                    var data = await reports.GetDowntimeReportAsync(fromUtc, toUtc, machineCode, cancellationToken);
                    var json = JsonSerializer.Serialize(data, JsonOpts);
                    evidenceList.Add(new AiDataEvidenceDto(
                        "GetDowntimeReport",
                        $"Machine downtime report from {fromUtc:yyyy-MM-dd} to {toUtc:yyyy-MM-dd}",
                        json));
                    return json;
                },
                "GetDowntimeReport",
                "Calculates exact durations across machine states (Running, Idle, Down, Setup, Maintenance) and availability percentage."),

            AIFunctionFactory.Create(
                async ([Description("Start of time range in UTC")] DateTime fromUtc,
                       [Description("End of time range in UTC")] DateTime toUtc,
                       [Description("Optional machine code filter")] string? machineCode) =>
                {
                    var data = await reports.GetOeeReportAsync(fromUtc, toUtc, machineCode, cancellationToken);
                    var json = JsonSerializer.Serialize(data, JsonOpts);
                    evidenceList.Add(new AiDataEvidenceDto(
                        "GetOeeReport",
                        $"OEE report from {fromUtc:yyyy-MM-dd} to {toUtc:yyyy-MM-dd}",
                        json));
                    return json;
                },
                "GetOeeReport",
                "Calculates Overall Equipment Effectiveness (OEE) with Availability, Performance, and Quality factors."),

            AIFunctionFactory.Create(
                async ([Description("Start of time range in UTC")] DateTime fromUtc,
                       [Description("End of time range in UTC")] DateTime toUtc) =>
                {
                    var data = await reports.GetRootCauseAnalysisAsync(fromUtc, toUtc, cancellationToken);
                    var json = JsonSerializer.Serialize(data, JsonOpts);
                    evidenceList.Add(new AiDataEvidenceDto(
                        "GetRootCauseAnalysis",
                        $"Automated root-cause anomaly detection ({data.IdentifiedRootCauses.Count} identified findings)",
                        json));
                    return json;
                },
                "GetRootCauseAnalysis",
                "Executes comprehensive root cause scan: recipe comparison yield drops, contact resistance drift on testers, and defect correlation."),

            AIFunctionFactory.Create(
                async ([Description("Serial number of the physical unit e.g. LOT-T1-001")] string serialNumber) =>
                {
                    var data = await traceQuery.GetUnitAsync(serialNumber, cancellationToken);
                    var json = JsonSerializer.Serialize(data, JsonOpts);
                    evidenceList.Add(new AiDataEvidenceDto(
                        "GetUnitTrace",
                        $"Traceability history for unit '{serialNumber}'",
                        json));
                    return json;
                },
                "GetUnitTrace",
                "Traces a single unit through every process step with recipe parameters, verdict, and sensor readings."),

            AIFunctionFactory.Create(
                async ([Description("Lot identifier e.g. LOT-0001")] string lotNumber) =>
                {
                    var data = await traceQuery.GetLotAsync(lotNumber, cancellationToken);
                    var json = JsonSerializer.Serialize(data, JsonOpts);
                    evidenceList.Add(new AiDataEvidenceDto(
                        "GetLotSummary",
                        $"Summary for lot '{lotNumber}'",
                        json));
                    return json;
                },
                "GetLotSummary",
                "Retrieves lot details including product code, start time, total units, pass count, and yield."),

            AIFunctionFactory.Create(
                async () =>
                {
                    var data = await traceQuery.GetMachinesAsync(cancellationToken);
                    var json = JsonSerializer.Serialize(data, JsonOpts);
                    evidenceList.Add(new AiDataEvidenceDto(
                        "GetPlantMachines",
                        $"Registered plant machines ({data.Count} machines)",
                        json));
                    return json;
                },
                "GetPlantMachines",
                "Lists all production machines, line assignments, types, and active states."),

            AIFunctionFactory.Create(
                async ([Description("Machine code e.g. FT-02, WB-01")] string machineCode,
                       [Description("Sensor or reading parameter name e.g. ContactResistance, PullStrength")] string parameterName,
                       [Description("Start of time range in UTC")] DateTime fromUtc,
                       [Description("End of time range in UTC")] DateTime toUtc) =>
                {
                    var data = await predictiveMaintenance.AnalyzeParameterAsync(machineCode, parameterName, fromUtc, toUtc, cancellationToken);
                    var json = JsonSerializer.Serialize(data, JsonOpts);
                    evidenceList.Add(new AiDataEvidenceDto(
                        "PredictiveMaintenanceScan",
                        $"ML.NET time-series anomaly analysis for {machineCode} '{parameterName}': Status {data.HealthStatus}, ChangePoint: {data.ChangePointDetected}",
                        json));
                    return json;
                },
                "PredictiveMaintenanceScan",
                "Runs ML.NET time-series change point and spike anomaly detection on sensor/test readings to detect equipment degradation and recommend proactive maintenance."),

            AIFunctionFactory.Create(
                async ([Description("Start of time range in UTC")] DateTime fromUtc,
                       [Description("End of time range in UTC")] DateTime toUtc) =>
                {
                    var data = await predictiveMaintenance.GetFleetHealthSummaryAsync(fromUtc, toUtc, cancellationToken);
                    var json = JsonSerializer.Serialize(data, JsonOpts);
                    evidenceList.Add(new AiDataEvidenceDto(
                        "GetFleetHealthSummary",
                        $"ML.NET fleet-wide machine health summary ({data.Count} machines)",
                        json));
                    return json;
                },
                "GetFleetHealthSummary",
                "Evaluates ML.NET time-series health across all factory machines, identifying active drifts, anomalies, and maintenance priorities.")
        };

        // 2. Wrap client with function invocation middleware so tools run automatically
        var executingClient = chatClient.AsBuilder()
            .UseFunctionInvocation()
            .Build();

        var systemPrompt = """
            You are the FabTrace Senior Plant & Yield Analysis AI Assistant for semiconductor assembly and test.
            You have access to read-only factory diagnostic tools to investigate yield, machine downtime, parametric drift, and defect pareto.

            Operational Rules:
            1. ALWAYS call tools with parameterized queries to retrieve real factory ground truth before answering.
            2. Never invent or hallucinate data; every metric must come from a tool call.
            3. Structure your response with concise bullet points and direct technical terms.
            4. Clearly highlight the root cause, affected machines, recipes, lots, and dates.
            5. Provide actionable engineering recommendations.
            """;

        var defaultFrom = query.FromUtc ?? DateTime.UtcNow.AddDays(-30);
        var defaultTo = query.ToUtc ?? DateTime.UtcNow;

        var userMessage = $"""
            Question: {query.Question}
            Timeframe: {defaultFrom:yyyy-MM-dd HH:mm:ss} UTC to {defaultTo:yyyy-MM-dd HH:mm:ss} UTC
            Machine filter: {query.MachineCode ?? "None (All Machines)"}
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userMessage),
        };

        var chatOptions = new ChatOptions
        {
            Tools = tools,
        };

        try
        {
            var response = await executingClient.GetResponseAsync(messages, chatOptions, cancellationToken);
            var answerText = response.Text ?? "No text response received from AI model.";
            return new AiRootCauseResponseDto(answerText, evidenceList);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or OperationCanceledException)
        {
            // Fallback when local Ollama is offline or unconfigured: run deterministic root cause engine directly
            var fallbackData = await reports.GetRootCauseAnalysisAsync(defaultFrom, defaultTo, cancellationToken);
            evidenceList.Add(new AiDataEvidenceDto(
                "AutomatedRootCauseFallback",
                "Deterministic root-cause anomaly detection executed as fallback (Ollama local service offline)",
                JsonSerializer.Serialize(fallbackData, JsonOpts)));

            var findingsSummary = fallbackData.IdentifiedRootCauses.Count > 0
                ? string.Join("\n- ", fallbackData.IdentifiedRootCauses)
                : "No abnormal yield drop or machine drift detected in the specified timeframe.";

            var fallbackAnswer = $"""
                [Note: Local Ollama service is unreachable at http://localhost:11434. Displaying data directly from FabTrace automated root-cause detection engine.]

                ### Root-Cause Findings
                - {findingsSummary}

                ### Data Summary
                - Yield records analyzed: {fallbackData.RecipeComparisons.Count}
                - Defect Pareto categories: {fallbackData.DefectPareto.Count}
                - Parametric drift points: {fallbackData.ParametricDrifts.Count}
                """;

            return new AiRootCauseResponseDto(fallbackAnswer, evidenceList);
        }
    }
}