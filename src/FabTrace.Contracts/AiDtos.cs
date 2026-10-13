namespace FabTrace.Contracts;

public sealed record AiRootCauseQueryDto
{
    public required string Question { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? MachineCode { get; init; }
}

public sealed record AiDataEvidenceDto(
    string ToolName,
    string Summary,
    string RawDataJson);

public sealed record AiRootCauseResponseDto(
    string Answer,
    IReadOnlyList<AiDataEvidenceDto> Evidence);