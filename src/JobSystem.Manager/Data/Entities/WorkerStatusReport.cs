namespace JobSystem.Manager.Data.Entities;

public class WorkerStatusReport
{
    public long Id { get; set; }
    public Guid WorkerInstanceId { get; set; }
    public WorkerReportStatus Status { get; set; }
    public string? ItemId { get; set; }
    public int? ProgressPercent { get; set; }
    public string? Message { get; set; }
    public DateTime ReportedAt { get; set; }

    // Navigation property
    public WorkerInstance WorkerInstance { get; set; } = null!;
}

public enum WorkerReportStatus
{
    Started,
    Processing,
    Completed,
    Failed,
    Heartbeat
}
