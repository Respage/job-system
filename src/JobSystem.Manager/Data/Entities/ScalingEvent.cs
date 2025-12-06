namespace JobSystem.Manager.Data.Entities;

public class ScalingEvent
{
    public long Id { get; set; }
    public Guid JobDefinitionId { get; set; }
    public ScalingEventType EventType { get; set; }
    public int PreviousWorkerCount { get; set; }
    public int NewWorkerCount { get; set; }
    public int QueueDepth { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAt { get; set; }

    // Navigation property
    public JobDefinition JobDefinition { get; set; } = null!;
}

public enum ScalingEventType
{
    ScaleUp,
    ScaleDown
}
