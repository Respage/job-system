namespace JobSystem.Manager.Data.Entities;

public class CodeSyncHistory
{
    public long Id { get; set; }
    public Guid JobDefinitionId { get; set; }
    public string? GitCommitHash { get; set; }
    public CodeSyncStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation property
    public JobDefinition JobDefinition { get; set; } = null!;
}

public enum CodeSyncStatus
{
    InProgress,
    Completed,
    Failed
}
