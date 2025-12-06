namespace JobSystem.Manager.Data.Entities;

public class WorkerInstance
{
    public Guid Id { get; set; }
    public Guid JobDefinitionId { get; set; }
    public string Ec2InstanceId { get; set; } = string.Empty;
    public string? PrivateIpAddress { get; set; }
    public string InstanceType { get; set; } = string.Empty;
    public bool IsSpotInstance { get; set; }
    public WorkerStatus Status { get; set; }
    public DateTime LaunchedAt { get; set; }
    public DateTime? LastHeartbeatAt { get; set; }
    public DateTime? TerminatedAt { get; set; }

    // Navigation properties
    public JobDefinition JobDefinition { get; set; } = null!;
    public ICollection<WorkerStatusReport> StatusReports { get; set; } = new List<WorkerStatusReport>();
}

public enum WorkerStatus
{
    Pending,
    Running,
    Terminating,
    Terminated
}
