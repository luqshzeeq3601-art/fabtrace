using FabTrace.Contracts;

namespace FabTrace.Core.Services;

public interface IAiRootCauseAssistant
{
    Task<AiRootCauseResponseDto> AnalyzeAsync(AiRootCauseQueryDto query, CancellationToken cancellationToken = default);
}