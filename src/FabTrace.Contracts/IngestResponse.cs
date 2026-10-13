namespace FabTrace.Contracts;

/// <summary>
/// The outcome for every item in a batch, in the order it was sent. A sender with
/// a store-and-forward queue can delete everything that is not a transport error:
/// Created and Duplicate are safely stored, Rejected will never succeed.
/// </summary>
public sealed record IngestResponse
{
    public required IReadOnlyList<IngestItemResult> Items { get; init; }

    public int Created => Items.Count(i => i.Status == IngestStatus.Created);
    public int Duplicates => Items.Count(i => i.Status == IngestStatus.Duplicate);
    public int Rejected => Items.Count(i => i.Status == IngestStatus.Rejected);
}

public sealed record IngestItemResult(Guid Id, IngestStatus Status, string? Error = null);