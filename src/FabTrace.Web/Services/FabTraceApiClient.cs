using System.Net.Http.Headers;
using System.Net.Http.Json;

using FabTrace.Contracts;

namespace FabTrace.Web.Services;

public sealed class FabTraceApiClient(HttpClient http)
{
    public async Task<LoginResponse?> LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest
            {
                UserName = userName,
                Password = password
            }, cancellationToken);

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<LiveFloorSummaryDto?> GetLiveSummaryAsync(string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/scada/live");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<LiveFloorSummaryDto>(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<UnitTraceDto?> GetUnitTraceAsync(string serialNumber, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/units/{Uri.EscapeDataString(serialNumber)}");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<UnitTraceDto>(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<LotSummaryDto?> GetLotSummaryAsync(string lotNumber, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/lots/{Uri.EscapeDataString(lotNumber)}");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<LotSummaryDto>(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IssueApiKeyResponse?> IssueApiKeyAsync(string machineCode, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/admin/machines/{Uri.EscapeDataString(machineCode)}/api-key");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<IssueApiKeyResponse>(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<YieldReportItemDto>?> GetYieldReportAsync(DateTime from, DateTime to, string? machineCode = null, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"api/reports/yield?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}" +
                      (!string.IsNullOrEmpty(machineCode) ? $"&machineCode={Uri.EscapeDataString(machineCode)}" : "");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<IReadOnlyList<YieldReportItemDto>>(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<OeeReportItemDto>?> GetOeeReportAsync(DateTime from, DateTime to, string? machineCode = null, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"api/reports/oee?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}" +
                      (!string.IsNullOrEmpty(machineCode) ? $"&machineCode={Uri.EscapeDataString(machineCode)}" : "");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<IReadOnlyList<OeeReportItemDto>>(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<MachineDowntimeItemDto>?> GetDowntimeReportAsync(DateTime from, DateTime to, string? machineCode = null, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"api/reports/downtime?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}" +
                      (!string.IsNullOrEmpty(machineCode) ? $"&machineCode={Uri.EscapeDataString(machineCode)}" : "");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<IReadOnlyList<MachineDowntimeItemDto>>(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<RootCauseAnalysisDto?> GetRootCauseAnalysisAsync(DateTime from, DateTime to, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"api/reports/root-cause?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<RootCauseAnalysisDto>(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<AiRootCauseResponseDto?> AnalyzeWithAiAsync(
        AiRootCauseQueryDto query, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/ai/analyze")
            {
                Content = JsonContent.Create(query),
            };
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<AiRootCauseResponseDto>(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<PredictiveMaintenanceReportDto?> GetPredictiveMaintenanceReportAsync(
        string machineCode, string parameterName, DateTime from, DateTime to, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"api/maintenance/predictive/machine/{Uri.EscapeDataString(machineCode)}?parameterName={Uri.EscapeDataString(parameterName)}&from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<PredictiveMaintenanceReportDto>(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<MachineHealthSummaryDto>?> GetFleetHealthSummaryAsync(
        DateTime from, DateTime to, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"api/maintenance/predictive/fleet-summary?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<IReadOnlyList<MachineHealthSummaryDto>>(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }
}