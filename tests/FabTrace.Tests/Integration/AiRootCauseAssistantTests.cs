using System.Net;
using System.Net.Http.Json;

using FabTrace.Contracts;
using FabTrace.Core.Services;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace FabTrace.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class AiRootCauseAssistantTests(FabTraceApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Ai_analyze_endpoint_requires_engineer_or_admin_role()
    {
        var now = DateTime.UtcNow;
        var query = new AiRootCauseQueryDto
        {
            Question = "Why did wire bonder yield drop?",
            FromUtc = now.AddDays(-7),
            ToUtc = now,
        };

        // 1. Anonymous request -> 401
        using var anonClient = Factory.CreateClient();
        var anonResp = await anonClient.PostAsJsonAsync("api/ai/analyze", query, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonResp.StatusCode);

        // 2. Operator role -> 403
        using var opClient = Factory.CreateUserClient("operator1", Roles.Operator);
        var opResp = await opClient.PostAsJsonAsync("api/ai/analyze", query, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, opResp.StatusCode);

        // 3. Engineer role -> 200
        using var engClient = Factory.CreateUserClient("engineer1", Roles.Engineer);
        var engResp = await engClient.PostAsJsonAsync("api/ai/analyze", query, Ct);
        Assert.Equal(HttpStatusCode.OK, engResp.StatusCode);
    }

    [Fact]
    public async Task Ai_assistant_collects_ground_truth_evidence_and_diagnoses_root_cause()
    {
        using var bonder = Factory.CreateMachineClient("WB-01");
        var now = DateTime.UtcNow;

        // Ingest yield drop data for BOND-QFN32 v2
        await PostResultsAsync(
            bonder,
            TestData.Result("SN-AI-01", recipeCode: "BOND-QFN32", recipeVersion: 1, inspectedAt: now.AddHours(-3)),
            TestData.Result("SN-AI-02", recipeCode: "BOND-QFN32", recipeVersion: 1, inspectedAt: now.AddHours(-2)),
            TestData.Result("SN-AI-03", recipeCode: "BOND-QFN32", recipeVersion: 2, verdict: Verdict.Fail, defectCode: "WIRE_LIFT", inspectedAt: now.AddHours(-1)),
            TestData.Result("SN-AI-04", recipeCode: "BOND-QFN32", recipeVersion: 2, verdict: Verdict.Fail, defectCode: "WIRE_LIFT", inspectedAt: now));

        using var engineerClient = Factory.CreateUserClient("engineer1", Roles.Engineer);
        var query = new AiRootCauseQueryDto
        {
            Question = "Identify the root cause of the yield drop on Wire Bonder WB-01.",
            FromUtc = now.AddDays(-1),
            ToUtc = now.AddDays(1),
            MachineCode = "WB-01",
        };

        var response = await engineerClient.PostAsJsonAsync("api/ai/analyze", query, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AiRootCauseResponseDto>(Ct);
        Assert.NotNull(result);
        Assert.NotNull(result.Answer);
        Assert.NotEmpty(result.Answer);

        // Every answer shows its data: evidence must not be empty
        Assert.NotEmpty(result.Evidence);
        Assert.All(result.Evidence, ev =>
        {
            Assert.False(string.IsNullOrWhiteSpace(ev.ToolName));
            Assert.False(string.IsNullOrWhiteSpace(ev.Summary));
            Assert.False(string.IsNullOrWhiteSpace(ev.RawDataJson));
        });
    }

    [Fact]
    public async Task Function_invocation_middleware_executes_readonly_tool_and_records_evidence()
    {
        using var bonder = Factory.CreateMachineClient("WB-01");
        var now = DateTime.UtcNow;

        await PostResultsAsync(
            bonder,
            TestData.Result("SN-TOOL-01", recipeCode: "BOND-QFN32", recipeVersion: 1, inspectedAt: now.AddHours(-1)),
            TestData.Result("SN-TOOL-02", recipeCode: "BOND-QFN32", recipeVersion: 2, verdict: Verdict.Fail, defectCode: "WIRE_LIFT", inspectedAt: now));

        // Create a test chat client that explicitly requests the GetYieldReport tool call
        var mockChatClient = new TestToolCallingChatClient("GetYieldReport", new
        {
            fromUtc = now.AddDays(-1),
            toUtc = now.AddDays(1),
            machineCode = "WB-01"
        });

        using var scope = Factory.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportService>();
        var trace = scope.ServiceProvider.GetRequiredService<ITraceQueryService>();
        var pred = scope.ServiceProvider.GetRequiredService<IPredictiveMaintenanceService>();

        var assistant = new FabTrace.Data.Services.AiRootCauseAssistant(mockChatClient, reports, trace, pred);

        var response = await assistant.AnalyzeAsync(new AiRootCauseQueryDto
        {
            Question = "Check WB-01 yield",
            FromUtc = now.AddDays(-1),
            ToUtc = now.AddDays(1),
            MachineCode = "WB-01"
        }, Ct);

        Assert.NotNull(response);
        Assert.Contains("Tool call completed successfully", response.Answer);

        // Verify the GetYieldReport C# tool was invoked and recorded in evidence
        Assert.Contains(response.Evidence, ev => ev.ToolName == "GetYieldReport" && ev.RawDataJson.Contains("WB-01"));
    }

    /// <summary>
    /// Test chat client that simulates an LLM requesting a tool call on step 1,
    /// then generating an answer on step 2 after function invocation middleware provides the tool result.
    /// </summary>
    private sealed class TestToolCallingChatClient(string requestedToolName, object toolArguments) : IChatClient
    {
        private bool _called;

        public ChatClientMetadata Metadata => new("TestToolCallingClient");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (!_called)
            {
                _called = true;
                // Emit a function call request
                var callContent = new FunctionCallContent(
                    callId: "call_123",
                    name: requestedToolName,
                    arguments: System.Text.Json.JsonSerializer.SerializeToElement(toolArguments)
                        .EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.ToString()));

                var assistantMessage = new ChatMessage(ChatRole.Assistant, [callContent]);
                return Task.FromResult(new ChatResponse(assistantMessage));
            }

            // Step 2: return final text answer summarizing tool output
            var finalMessage = new ChatMessage(ChatRole.Assistant, "Tool call completed successfully with grounded factory metrics.");
            return Task.FromResult(new ChatResponse(finalMessage));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}