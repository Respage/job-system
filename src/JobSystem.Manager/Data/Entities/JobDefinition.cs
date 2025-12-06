namespace JobSystem.Manager.Data.Entities;

public class JobDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JobLanguage Language { get; set; }
    public QueueType QueueType { get; set; }
    public string QueueConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public InstanceSize InstanceSize { get; set; }
    public bool UseSpotInstances { get; set; } = true;
    public ScaleMode ScaleMode { get; set; }
    public int MinWorkers { get; set; }
    public int MaxWorkers { get; set; } = 10;
    public int QueueItemTimeoutSeconds { get; set; } = 300;
    public int QueuePollingIntervalSeconds { get; set; } = 30;
    public string GitRepoUrl { get; set; } = string.Empty;
    public string GitBranch { get; set; } = "main";
    public string S3CodePath { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<WorkerInstance> WorkerInstances { get; set; } = new List<WorkerInstance>();
    public ICollection<ScalingEvent> ScalingEvents { get; set; } = new List<ScalingEvent>();
    public ICollection<CodeSyncHistory> CodeSyncHistory { get; set; } = new List<CodeSyncHistory>();
}

public enum JobLanguage
{
    Node,
    CSharp
}

public enum QueueType
{
    SQS,
    MongoDB
}

public enum InstanceSize
{
    Small,
    Medium,
    Large
}

public enum ScaleMode
{
    Aggressive,
    Gradual
}
